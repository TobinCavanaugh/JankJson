namespace parseJson;

public enum JTokenType {
    // @formatter:off
    Whitespace, Newline, Null, Colon, Comma,
    Word,    /*  "..."  */
    Num,     /*    1    */
    Boolean, /*  false  */
    CurlOp,  /*    {    */
    CurlCl,  /*    }    */
    SqOp,    /*    [    */
    SqCl,    /*    ]    */
    // @formatter:on
}

/// <summary>
/// Type for lexing
/// </summary>
public class JToken {
    public JTokenType Type;
    public string Value;

    public int Row, Column;

    public JToken(JTokenType type, string value, int row, int column) {
        Type = type;
        Value = value;
        Row = row;
        Column = column;
    }

    public override string ToString() {
        return $"{Type} : `{(Value == "\n" ? "\\n" : Value)}`";
    }

    public string Pos() {
        return $"{Row}:{Column}";
    }
}

public class JLexer {
    static int MatchNumber(ReadOnlySpan<char> str, out string number) {
        number = "";
        int i = 0;

        if (i >= str.Length) return 0;

        // Optional minus sign
        if (str[i] == '-') {
            i++;
        }

        if (i >= str.Length || !char.IsDigit(str[i])) return 0;

        // Integer part
        if (str[i] == '0') {
            i++; // Just zero
        }
        else {
            // 1-9 followed by digits
            while (i < str.Length && char.IsDigit(str[i])) {
                i++;
            }
        }

        // Optional fractional part
        if (i < str.Length && str[i] == '.') {
            i++;
            if (i >= str.Length || !char.IsDigit(str[i])) return 0; // Must have digit after decimal
            while (i < str.Length && char.IsDigit(str[i])) {
                i++;
            }
        }

        // Optional exponent part
        if (i < str.Length && (str[i] == 'e' || str[i] == 'E')) {
            i++;
            if (i < str.Length && (str[i] == '+' || str[i] == '-')) {
                i++;
            }

            if (i >= str.Length || !char.IsDigit(str[i])) return 0; // Must have digit after exponent
            while (i < str.Length && char.IsDigit(str[i])) {
                i++;
            }
        }

        number = new string(str.Slice(0, i));
        return i;
    }

    public static List<JToken> Lex(string content) {
        int line = 1;
        int col = 1;

        List<JToken> tokens = new() { new JToken(JTokenType.Whitespace, " ", line, col) };

        bool inQuotes = false;
        bool escaped = false;

        for (int i = 0; i < content.Length; i++) {
            var c = content[i];
            var rem = content.AsSpan(i);
            var prev = tokens[^1];

            // Track current position before processing character
            int currentLine = line;
            int currentCol = col;

            col++;

            var AddTokenHere = (JTokenType type, string value) => {
                tokens.Add(new JToken(type, value, currentLine, currentCol));
            };

            if (!inQuotes) {
                // @formatter:off
                if (c == ':')  { AddTokenHere(JTokenType.Colon,   ":"  ); continue; }
                if (c == '{')  { AddTokenHere(JTokenType.CurlOp,  "{"  ); continue; }
                if (c == '}')  { AddTokenHere(JTokenType.CurlCl,  "}"  ); continue; }
                if (c == '[')  { AddTokenHere(JTokenType.SqOp,    "["  ); continue; }
                if (c == ']')  { AddTokenHere(JTokenType.SqCl,    "]"  ); continue; }
                if (c == ',')  { AddTokenHere(JTokenType.Comma,   ","  ); continue; }
                if (c == '\n') { line++; col = 0; }
                // @formatter:on

                // If we already have a whitespace, just increment the size of it
                if (char.IsWhiteSpace(c)) {
                    if (prev.Type == JTokenType.Whitespace) {
                        continue;
                    }

                    AddTokenHere(JTokenType.Whitespace, " ");
                    continue;
                }
            }

            if (inQuotes) {
                if (c == '\\' && !escaped) {
                    escaped = true;
                    // Add the backslash to the current word
                    if (prev.Type == JTokenType.Word) prev.Value += c;
                    else AddTokenHere(JTokenType.Word, c.ToString());

                    continue;
                }

                // If we're entering / exiting a quote
                if (c == '"' && !escaped) {
                    inQuotes = false;
                    // Process escape sequences in the completed string
                    if (prev.Type == JTokenType.Word) {
                        prev.Value = StringExt.UnescapeString(prev.Value);
                    }

                    continue;
                }

                // Reset escaped flag after processing any character following a backslash
                if (escaped) {
                    escaped = false;
                }
            }
            else {
                // If we're entering a quote
                if (c == '"') {
                    inQuotes = true;
                    continue;
                }
            }

            // If we are in a quote, we are building a word
            if (inQuotes) {
                if (prev.Type == JTokenType.Word) {
                    prev.Value += c;
                    continue;
                }

                AddTokenHere(JTokenType.Word, c.ToString());
            }
            else {
                { // Num
                    // string num = Match(rem, "-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][-+]?[0-9]+)?");
                    // var num = Match(rem, "");

                    // Try to match number
                    int numLength = MatchNumber(rem, out string number);
                    if (numLength > 0) {
                        AddTokenHere(JTokenType.Num, number);
                        i += numLength - 1;
                        col += numLength - 1;
                        continue;
                    }
                }

                // Handles stuff like false, true, null
                var UnquotedStringToType = (ReadOnlySpan<char> _rem, string match, JTokenType type, ref int ind,
                    out bool shouldContinue) => {
                    if (_rem.StartsWith(match, StringComparison.OrdinalIgnoreCase)) {
                        AddTokenHere(type, match);
                        ind += match.Length - 1;
                        col += match.Length - 1;
                        shouldContinue = true;
                        return;
                    }

                    shouldContinue = false;
                };

                bool con = false;

                UnquotedStringToType(rem, "false", JTokenType.Boolean, ref i, out con);
                if (con) continue;

                UnquotedStringToType(rem, "true", JTokenType.Boolean, ref i, out con);
                if (con) continue;

                UnquotedStringToType(rem, "null", JTokenType.Null, ref i, out con);
                if (con) continue;

                // Technically this should be a failure state
                Console.Error.WriteLine($"Unexpected character '{c}' at {currentLine}:{currentCol}");
            }
        }

        // Keeping whitespace etc is redundant
        tokens.RemoveAll(x => x.Type == JTokenType.Whitespace || x.Type == JTokenType.Newline);

        return tokens;
    }
}