using System.Text.RegularExpressions;

namespace parseJson;

public enum JTokenType {
    Whitespace,
    Newline,
    Word, // "..."
    Num, // 1
    Boolean, // false
    Null,
    Colon,
    Comma,
    CurlOp, //{
    CurlCl, //}
    SqOp, //[
    SqCl, //]
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
    // Just does a regex match, will return "" if it fails, otherwise it will return the matched area
    static string Match(string str, string regex) {
        try {
            Match m = Regex.Match(str, regex);
            if (m.Success) {
                return m.Value;
            }
        }
        catch (Exception e) { }

        return "";
    }

    public static List<JToken> Lex(string content) {
        int line = 0;
        int col = 0;

        List<JToken> tokens = new() { new JToken(JTokenType.Whitespace, " ", line, col) };

        bool inQuotes = false;
        bool escaped = false;

        for (int i = 0; i < content.Length; i++) {
            var c = content[i];
            var rem = content.Substring(i);
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
                    if (prev.Type == JTokenType.Word) {
                        prev.Value += c;
                    }
                    else {
                        AddTokenHere(JTokenType.Word, c.ToString());
                    }

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
                    string num = Match(rem, "-?(0|[1-9][0-9]*)(\\.[0-9]+)?([eE][-+]?[0-9]+)?");

                    // If we found a num
                    if (num != "" && rem.StartsWith(num)) {
                        AddTokenHere(JTokenType.Num, num);
                        i += num.Length - 1;
                        col += num.Length - 1;
                        continue;
                    }
                }

                // Handles stuff like false, true, null
                var UnquotedStringToType = (string match, JTokenType type, ref int ind, out bool shouldContinue) => {
                    if (rem.StartsWith(match, StringComparison.OrdinalIgnoreCase)) {
                        AddTokenHere(type, match);
                        ind += match.Length - 1;
                        col += match.Length - 1;
                        shouldContinue = true;
                        return;
                    }

                    shouldContinue = false;
                };

                bool con = false;

                UnquotedStringToType("false", JTokenType.Boolean, ref i, out con);
                if (con) continue;

                UnquotedStringToType("true", JTokenType.Boolean, ref i, out con);
                if (con) continue;

                UnquotedStringToType("null", JTokenType.Null, ref i, out con);
                if (con) continue;

                Console.Error.WriteLine($"Unexpected character '{c}' at {currentLine}:{currentCol}");
            }
        }

        // Keeping whitespace etc is redundant
        tokens.RemoveAll(x => x.Type == JTokenType.Whitespace || x.Type == JTokenType.Newline);

        return tokens;
    }
}