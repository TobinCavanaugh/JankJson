using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JJson;

public static class StringExt {
    public static bool StartsWithInsensitive(this ReadOnlySpan<char> span, string str) {
        return span.CompareTo(str.AsSpan(str.Length), StringComparison.InvariantCultureIgnoreCase) == 0;
    }

    public static string UnescapeString(string input) {
        return UnescapeString(input.AsSpan()).ToString();
    }

    public static string UnescapeString(StringBuilder sb) {
        StringBuilder ironic = new(sb.Length);
        foreach (var chunk in sb.GetChunks()) {
            ironic.Append(UnescapeString(chunk.Span));
        }

        return ironic.ToString();
    }

    public static ReadOnlySpan<char> UnescapeString(ReadOnlySpan<char> input) {
        // return Regex.Unescape(input);
        // if (string.IsNullOrEmpty(input)) return "";
        if (input.IsEmpty) return "";

        var result = new StringBuilder();

        for (int i = 0; i < input.Length; i++) {
            if (input[i] == '\\' && i + 1 < input.Length) {
                char nextChar = input[i + 1];
                switch (nextChar) {
                    case '"':
                        result.Append('"');
                        i++; // Skip the next character
                        break;
                    case '\\':
                        result.Append('\\');
                        i++;
                        break;
                    case '/':
                        result.Append('/');
                        i++;
                        break;
                    case 'b':
                        result.Append('\b');
                        i++;
                        break;
                    case 'f':
                        result.Append('\f');
                        i++;
                        break;
                    case 'n':
                        result.Append('\n');
                        i++;
                        break;
                    case 'r':
                        result.Append('\r');
                        i++;
                        break;
                    case 't':
                        result.Append('\t');
                        i++;
                        break;
                    case 'u':
                        // Unicode escape sequence \uXXXX
                        if (i + 5 < input.Length) {
                            // string hexCode = input.Substring(i + 2, 4);
                            var hexCode = input.Slice(i + 2, 4);
                            if (int.TryParse(hexCode, NumberStyles.HexNumber, null,
                                    out int unicodeValue)) {
                                result.Append((char)unicodeValue);
                                i += 5; // Skip \uXXXX
                            }
                            else {
                                // Invalid unicode escape, keep as is
                                result.Append(input[i]);
                            }
                        }
                        else {
                            // Incomplete unicode escape, keep as is
                            result.Append(input[i]);
                        }

                        break;
                    default:
                        // Unknown escape sequence, keep the backslash
                        result.Append(input[i]);
                        break;
                }
            }
            else {
                result.Append(input[i]);
            }
        }

        return result.ToString();
    }

    public static string EscapeString(string input) {
        // return Regex.Escape(input);
        if (string.IsNullOrEmpty(input)) return input;

        var result = new StringBuilder();

        foreach (char c in input) {
            switch (c) {
                case '"':
                    result.Append("\\\"");
                    break;
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\b':
                    result.Append("\\b");
                    break;
                case '\f':
                    result.Append("\\f");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (c < 32 || c > 126) {
                        // Escape non-printable characters as unicode
                        result.Append($"\\u{(int)c:X4}");
                    }
                    else {
                        result.Append(c);
                    }

                    break;
            }
        }

        return result.ToString();
    }
}