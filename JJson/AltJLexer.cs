using System.Text;
using System.Collections.Concurrent;

namespace JJson;

public class ThreadLexWorkerOptimized {
    public int StartIndex { get; }
    public int Size { get; }
    public int ThreadIndex { get; }
    public bool Complete = false;
    
    // Results are now final JTokens, not precursor tokens
    public List<JToken> Results = new();
    
    // Boundary state info for merging
    public bool StartsInQuote = false;
    public bool EndsInQuote = false;
    public int QuoteDepth = 0; // Track escape state
    public string? PartialTokenAtStart = null;
    public string? PartialTokenAtEnd = null;
    public JTokenType PartialTokenTypeAtEnd = JTokenType.Word;

    public ThreadLexWorkerOptimized(int threadIndex, int startIndex, int size) {
        StartIndex = startIndex;
        Size = size;
        ThreadIndex = threadIndex;
    }

    public void Work(ReadOnlySpan<char> data) {
        Complete = false;
        Results.Clear();
        Results.EnsureCapacity(Size / 4); // Better estimate

        var content = data.Slice(StartIndex, Size);
        
        int pos = 0;
        int line = 1; // Will be recalculated later
        int col = 1;

        // Check if we start inside a quote by looking backwards
        if (ThreadIndex > 0) {
            StartsInQuote = DetermineIfStartsInQuote(data, StartIndex);
        }

        while (pos < content.Length) {
            char c = content[pos];

            // Skip whitespace
            if (char.IsWhiteSpace(c)) {
                if (c == '\n') { line++; col = 1; }
                else if (c != '\r') { col++; }
                pos++;
                continue;
            }

            int tokenLine = line;
            int tokenCol = col;

            // If we're starting in a quote, handle specially
            if (pos == 0 && StartsInQuote) {
                var (consumed, token) = LexContinuedString(content, pos, tokenLine, tokenCol);
                if (token != null) Results.Add(token);
                pos += consumed;
                col += consumed;
                continue;
            }

            switch (c) {
                case '{': AddStructuralToken(JTokenType.CurlOp, "{", tokenLine, tokenCol); pos++; col++; break;
                case '}': AddStructuralToken(JTokenType.CurlCl, "}", tokenLine, tokenCol); pos++; col++; break;
                case '[': AddStructuralToken(JTokenType.SqOp, "[", tokenLine, tokenCol); pos++; col++; break;
                case ']': AddStructuralToken(JTokenType.SqCl, "]", tokenLine, tokenCol); pos++; col++; break;
                case ':': AddStructuralToken(JTokenType.Colon, ":", tokenLine, tokenCol); pos++; col++; break;
                case ',': AddStructuralToken(JTokenType.Comma, ",", tokenLine, tokenCol); pos++; col++; break;
                
                case '"':
                    var (stringConsumed, stringToken, endsInQuote) = LexString(content, pos, tokenLine, tokenCol);
                    if (stringToken != null) Results.Add(stringToken);
                    pos += stringConsumed;
                    col += stringConsumed;
                    if (pos >= content.Length && endsInQuote) {
                        EndsInQuote = true;
                    }
                    break;

                case '-':
                case '0': case '1': case '2': case '3': case '4':
                case '5': case '6': case '7': case '8': case '9':
                    var (numConsumed, numToken, isPartial) = LexNumber(content, pos, tokenLine, tokenCol);
                    if (numToken != null) Results.Add(numToken);
                    pos += numConsumed;
                    col += numConsumed;
                    if (pos >= content.Length && isPartial) {
                        PartialTokenAtEnd = numToken?.Value;
                        PartialTokenTypeAtEnd = JTokenType.Num;
                    }
                    break;

                default:
                    var (idConsumed, idToken, idIsPartial) = LexIdentifier(content, pos, tokenLine, tokenCol);
                    if (idToken != null) Results.Add(idToken);
                    pos += idConsumed;
                    col += idConsumed;
                    if (pos >= content.Length && idIsPartial) {
                        PartialTokenAtEnd = idToken?.Value;
                        PartialTokenTypeAtEnd = JTokenType.Word;
                    }
                    break;
            }
        }

        Complete = true;
    }

    private bool DetermineIfStartsInQuote(ReadOnlySpan<char> fullData, int startIndex) {
        // Look backwards to determine quote state
        bool inQuote = false;
        bool escaped = false;
        
        for (int i = startIndex - 1; i >= 0; i--) {
            char c = fullData[i];
            
            if (c == '\\' && !escaped) {
                escaped = true;
            } else if (c == '"' && !escaped) {
                inQuote = !inQuote;
                escaped = false;
            } else {
                escaped = false;
            }
            
            // Optimization: stop looking after we've gone far enough back
            if (startIndex - i > 1000) break; // Reasonable limit
        }
        
        return inQuote;
    }

    private (int consumed, JToken? token) LexContinuedString(ReadOnlySpan<char> content, int pos, int line, int col) {
        // We're continuing a string from the previous thread
        var sb = new StringBuilder();
        int start = pos;
        bool escaped = false;

        while (pos < content.Length) {
            char c = content[pos];
            
            if (c == '\\' && !escaped) {
                escaped = true;
                sb.Append(c);
            } else if (c == '"' && !escaped) {
                // Found the end of the string
                string value = sb.ToString();
                return (pos - start + 1, new JToken(JTokenType.Word, StringExt.UnescapeString(value), line, col));
            } else {
                escaped = false;
                sb.Append(c);
            }
            pos++;
        }

        // String continues into next thread
        PartialTokenAtEnd = sb.ToString();
        PartialTokenTypeAtEnd = JTokenType.Word;
        EndsInQuote = true;
        return (pos - start, null);
    }

    private (int consumed, JToken? token, bool endsInQuote) LexString(ReadOnlySpan<char> content, int pos, int line, int col) {
        pos++; // Skip opening quote
        int start = pos;
        var sb = new StringBuilder();
        bool hasEscapes = false;
        bool escaped = false;

        while (pos < content.Length) {
            char c = content[pos];

            if (c == '\\' && !escaped) {
                escaped = true;
                hasEscapes = true;
                sb.Append(c);
            } else if (c == '"' && !escaped) {
                // Found closing quote
                string value;
                if (!hasEscapes) {
                    value = content.Slice(start, pos - start).ToString();
                } else {
                    value = StringExt.UnescapeString(sb.ToString());
                }
                return (pos - start + 2, new JToken(JTokenType.Word, value, line, col), false);
            } else {
                escaped = false;
                if (hasEscapes) sb.Append(c);
            }
            pos++;
        }

        // String continues beyond this thread
        string partialValue = hasEscapes ? sb.ToString() : content.Slice(start, pos - start).ToString();
        PartialTokenAtEnd = partialValue;
        PartialTokenTypeAtEnd = JTokenType.Word;
        return (pos - start + 1, null, true);
    }

    private (int consumed, JToken? token, bool isPartial) LexNumber(ReadOnlySpan<char> content, int pos, int line, int col) {
        int start = pos;
        
        // Handle optional minus
        if (content[pos] == '-') pos++;
        
        // Integer part
        while (pos < content.Length && char.IsDigit(content[pos])) pos++;
        
        // Decimal part
        if (pos < content.Length && content[pos] == '.' && pos + 1 < content.Length && char.IsDigit(content[pos + 1])) {
            pos++; // Skip dot
            while (pos < content.Length && char.IsDigit(content[pos])) pos++;
        }
        
        // Exponent part
        if (pos < content.Length && (content[pos] == 'e' || content[pos] == 'E')) {
            pos++;
            if (pos < content.Length && (content[pos] == '+' || content[pos] == '-')) pos++;
            while (pos < content.Length && char.IsDigit(content[pos])) pos++;
        }

        // Check if number might continue (ends with digit and we're at thread boundary)
        bool isPartial = pos >= content.Length && char.IsDigit(content[pos - 1]);
        
        string value = content.Slice(start, pos - start).ToString();
        return (pos - start, new JToken(JTokenType.Num, value, line, col), isPartial);
    }

    private (int consumed, JToken? token, bool isPartial) LexIdentifier(ReadOnlySpan<char> content, int pos, int line, int col) {
        int start = pos;
        
        while (pos < content.Length && !char.IsWhiteSpace(content[pos]) && 
               "{}[],:\"".IndexOf(content[pos]) == -1) {
            pos++;
        }

        bool isPartial = pos >= content.Length && pos > start;
        string value = content.Slice(start, pos - start).ToString();
        
        JTokenType tokenType = value.ToLowerInvariant() switch {
            "true" or "false" => JTokenType.Boolean,
            "null" => JTokenType.Null,
            _ => JTokenType.Word
        };

        return (pos - start, new JToken(tokenType, value, line, col), isPartial);
    }

    private void AddStructuralToken(JTokenType type, string value, int line, int col) {
        Results.Add(new JToken(type, value, line, col));
    }
}

public class OptimizedParallelLexer {
    public static List<JToken> Lex(string content) {
        int threads = Math.Min(Environment.ProcessorCount, 8);
        var workers = CreateWorkers(content, threads);

        // Parallel processing
        Parallel.ForEach(workers, worker => {
            try {
                worker.Work(content.AsSpan());
            } catch (Exception ex) {
                Console.Error.WriteLine($"Error in thread {worker.ThreadIndex}: {ex.Message}");
            }
        });

        // Merge results and handle boundaries
        return MergeResults(workers, content);
    }

    private static List<ThreadLexWorkerOptimized> CreateWorkers(string content, int threads) {
        var workers = new List<ThreadLexWorkerOptimized>();
        int totalSize = content.Length;
        int sizePerThread = totalSize / threads;
        int remainder = totalSize % threads;
        int spanIndex = 0;

        for (int i = 0; i < threads; i++) {
            int size = sizePerThread + (i < remainder ? 1 : 0);
            workers.Add(new ThreadLexWorkerOptimized(i, spanIndex, size));
            spanIndex += size;
        }

        return workers;
    }

    private static List<JToken> MergeResults(List<ThreadLexWorkerOptimized> workers, string content) {
        var result = new List<JToken>();
        
        for (int i = 0; i < workers.Count; i++) {
            var worker = workers[i];
            
            // Handle boundary merging
            if (i > 0) {
                var prevWorker = workers[i - 1];
                
                // Check if there are tokens that need to be processed at the boundary
                int boundaryStart = prevWorker.StartIndex + prevWorker.Size;
                int boundaryEnd = Math.Min(worker.StartIndex + 10, content.Length); // Look ahead a bit
                
                // Process any tokens that might have been missed at the boundary
                var boundaryTokens = LexBoundary(content.AsSpan(boundaryStart, boundaryEnd - boundaryStart));
                
                // Merge partial tokens at boundaries
                if (prevWorker.PartialTokenAtEnd != null && worker.Results.Count > 0) {
                    var firstToken = worker.Results[0];
                    
                    // If both are the same type, merge them
                    if (prevWorker.PartialTokenTypeAtEnd == firstToken.Type) {
                        string mergedValue = prevWorker.PartialTokenAtEnd + firstToken.Value;
                        
                        // Replace the last token in result with merged version
                        if (result.Count > 0) {
                            result[result.Count - 1] = new JToken(firstToken.Type, mergedValue, 
                                result[result.Count - 1].Row, result[result.Count - 1].Column);
                        }
                        
                        // Add any boundary tokens we found
                        result.AddRange(boundaryTokens);
                        
                        // Skip the first token from current worker
                        result.AddRange(worker.Results.Skip(1));
                        continue;
                    }
                }
                
                // Add any boundary tokens we found
                result.AddRange(boundaryTokens);
            }
            
            result.AddRange(worker.Results);
        }

        return result;
    }
    
    private static List<JToken> LexBoundary(ReadOnlySpan<char> boundary) {
        var tokens = new List<JToken>();
        int pos = 0;
        
        while (pos < boundary.Length) {
            char c = boundary[pos];
            
            // Skip whitespace
            if (char.IsWhiteSpace(c)) {
                pos++;
                continue;
            }
            
            // Only look for structural tokens at boundaries
            switch (c) {
                case '{': tokens.Add(new JToken(JTokenType.CurlOp, "{", 1, 1)); pos++; break;
                case '}': tokens.Add(new JToken(JTokenType.CurlCl, "}", 1, 1)); pos++; break;
                case '[': tokens.Add(new JToken(JTokenType.SqOp, "[", 1, 1)); pos++; break;
                case ']': tokens.Add(new JToken(JTokenType.SqCl, "]", 1, 1)); pos++; break;
                case ':': tokens.Add(new JToken(JTokenType.Colon, ":", 1, 1)); pos++; break;
                case ',': tokens.Add(new JToken(JTokenType.Comma, ",", 1, 1)); pos++; break;
                default: 
                    // Skip non-structural characters - they should be handled by the main workers
                    pos++;
                    break;
            }
        }
        
        return tokens;
    }
}
