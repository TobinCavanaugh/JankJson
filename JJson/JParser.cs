using System.Diagnostics;

namespace JJson;

public struct JParserSettings {
    public bool AllowTrailingCommas = false;
    public bool AssignRawContent = true;
    public bool AssignParents = true;
    public bool MeasureParseTime = true;
    public JParserSettings() { }

    public override string ToString() {
        return
            $"{nameof(AllowTrailingCommas)}:{AllowTrailingCommas}, " +
            $"{nameof(AssignRawContent)}:{AssignRawContent}, " +
            $"{nameof(AssignParents)}:{AssignParents}, " +
            $"{nameof(MeasureParseTime)}:{MeasureParseTime}";
    }
}

public class JParser {
    private readonly List<JToken> _tokens;
    private int _position = 0;
    public readonly JParserSettings Settings;

    public JParser(List<JToken> tokens, JParserSettings? settings = null) {
        _tokens = tokens;
        if (settings == null) this.Settings = new();
        else this.Settings = settings.Value;

        Console.WriteLine(settings.ToString());
    }

    private JToken? Current => _position < _tokens.Count ? _tokens[_position] : null;
    private JToken? Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : null;

    // Eat a token if it's a certain type, otherwise throw
    private JToken? Consume(JTokenType expected) {
        if (Current?.Type != expected) {
            throw new Exception($"Expected {expected} but got {Current?.Type} at {Current?.Row}:{Current?.Column}");
        }

        return _tokens[_position++];
    }

    // Eat any token
    private JToken? ConsumeAny() {
        if (Current == null) {
            throw new Exception($"Unexpected end of stream {Current?.Pos()}");
        }

        return _tokens[_position++];
    }

    private void ResolveParentHood(JValue root) {
        if (root is JArray array) {
            foreach (var x in array.Elements) {
                x.Parent = root;
                ResolveParentHood(x);
            }
        }

        if (root is JObject obj) {
            foreach (var x in obj.Fields) {
                x.Value.Parent = root;
                ResolveParentHood(x.Value);
            }
        }
    }

    public Stopwatch Stopwatch = new();

    // The function to call to parse your json
    public JValue Parse() {
        if (Settings.MeasureParseTime) {
            Stopwatch.Reset();
            Stopwatch.Start();
        }

        var result = ParseValue();
        if (_position < _tokens.Count) {
            throw new Exception($"Parsing did not complete: {Current} {Current?.Pos()}");
        }

        if (Settings.AssignParents) ResolveParentHood(result);
        if (Settings.MeasureParseTime) Stopwatch?.Stop();

        return result;
    }

    delegate JValue JAnon(Action<JToken> token);
    // Called recursively within parsenull, parseobject, parsearray

    private JValue ParseValue() {
        if (Current == null) {
            throw new Exception($"Unexpected end of stream {Current?.Pos()}");
        }

        JValue result;

        switch (Current.Type) {
            case JTokenType.Word:
                var wordToken = ConsumeAny();
                result = new JString(wordToken.Value);
                if (Settings.AssignRawContent) result.RawContent = $"\"{wordToken.Value}\"";
                break;
            case JTokenType.Num:
                var numToken = ConsumeAny();
                result = new JNumber(double.Parse(numToken.Value));
                if (Settings.AssignRawContent) result.RawContent = numToken.Value;
                break;
            case JTokenType.Boolean:
                var boolToken = ConsumeAny();
                result = new JBoolean(bool.Parse(boolToken.Value));
                if (Settings.AssignRawContent) result.RawContent = boolToken.Value.ToLower();
                break;
            case JTokenType.Null:
                result = ParseNull();
                break;
            case JTokenType.CurlOp:
                result = ParseObject();
                break;
            case JTokenType.SqOp:
                result = ParseArray();
                break;
            default:
                throw new Exception($"Unexpected token {Current} {Current?.Pos()}");
        }

        return result;
    }

    private JValue ParseNull() {
        // lambdas not allowed in switch :(((((
        ConsumeAny();
        var result = new JNull();
        if (Settings.AssignRawContent) result.RawContent = "null";
        return result;
    }

    private JObject ParseObject() {
        // {
        var startPos = _position;
        Consume(JTokenType.CurlOp);
        var obj = new JObject();

        // Empty object
        if (Current?.Type == JTokenType.CurlCl) {
            Consume(JTokenType.CurlCl);
            if (Settings.AssignRawContent) obj.RawContent = "{}";
            return obj;
        }

        while (true) {
            if (Settings.AllowTrailingCommas) {
                if (Current?.Type == JTokenType.CurlCl) {
                    Consume(JTokenType.CurlCl);
                    break;
                }
                else {
                    throw new Exception($"Trailing Commas are not allowed. At {Current?.Pos()}");
                }
            }

            if (Current?.Type != JTokenType.Word) {
                throw new Exception($"Expected string key but got {Current} {Current?.Pos()}");
            }

            var key = ConsumeAny().Value; // "key"
            Consume(JTokenType.Colon); // ":"
            obj.Add(key, ParseValue()); // "value"

            if (Current?.Type == JTokenType.CurlCl) {
                // Done with obj
                Consume(JTokenType.CurlCl);
                break;
            }

            if (Current?.Type == JTokenType.Comma) {
                Consume(JTokenType.Comma);
                continue;
            }

            throw new Exception($"Expected `,` or `}}` but got {Current} {Current?.Pos()}");
        }

        if (Settings.AssignRawContent) {
            var pairs = obj.Fields.Select(kvp => $"\"{kvp.Key}\": {kvp.Value.RawContent}");
            obj.RawContent = "{" + string.Join(", ", pairs) + "}";
        }

        return obj;
    }

    private JArray ParseArray() {
        // [
        var startPos = _position;
        Consume(JTokenType.SqOp);
        var arr = new JArray();

        // Empty array
        if (Current?.Type == JTokenType.SqCl) {
            Consume(JTokenType.SqCl);
            if (Settings.AssignRawContent) arr.RawContent = "[]";
            return arr;
        }

        while (true) {
            if (Settings.AllowTrailingCommas) {
                if (Current?.Type == JTokenType.SqCl) {
                    Consume(JTokenType.SqCl);
                    break;
                }
                else {
                    throw new Exception($"Trailing Commas are not allowed. At {Current?.Pos()}");
                }
            }

            arr.Elements.Add(ParseValue());

            if (Current?.Type == JTokenType.SqCl) {
                Consume(JTokenType.SqCl);
                break;
            }

            if (Current?.Type == JTokenType.Comma) {
                Consume(JTokenType.Comma);
                continue;
            }

            throw new Exception($"Expected `,` or `]` but got {Current} {Current?.Pos()}");
        }

        if (Settings.AssignRawContent) {
            arr.RawContent = "[" + string.Join(", ", arr.Elements.Select(x => x.RawContent)) + "]";
        }

        return arr;
    }
}