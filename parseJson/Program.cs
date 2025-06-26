using System.Diagnostics;
using System.Text.RegularExpressions;

namespace parseJson;

public class Program {
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

    static List<JToken> Lex(string content) {
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

            // Handle newlines first (before incrementing col)
            if (!inQuotes && c == '\n') {
                tokens.Add(new JToken(JTokenType.Newline, "\n", currentLine, currentCol));
                ++line;
                col = 0;
                continue;
            }

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

            // If we're entering / exiting a quote
            if (!escaped) {
                if (c == '"') {
                    inQuotes = !inQuotes;
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

    public abstract class JValue {
        public abstract override string ToString();

        // Having setters would be nice...
        public virtual JValue this[string key] {
            get {
                if (this is JObject obj && obj.Fields.ContainsKey(key)) {
                    return obj.Fields[key];
                }

                // could also throw :|
                // return new JNull();
                throw new Exception($"String indexer called on non JObject type: `{this.GetType()}`");
            }
        }

        public virtual JValue this[int index] {
            get {
                if (this is JArray arr && Math.Clamp(index, 0, arr.Elements.Count - 1) == index) {
                    return arr.Elements[index];
                }

                // return new JNull();
                throw new Exception($"String indexer called on non JArray type: `{this.GetType()}`");
            }
        }

        // Maybe should be explicit?? Also should maybe use As... though throwing on implicit conversions is sus
        public static implicit operator string(JValue value) {
            return value.As<string>() ?? "";
            // return value is JString jstr ? jstr.Value : "";
        }

        public static implicit operator double(JValue value) {
            return value is JNumber jnum ? jnum.Value : 0.0;
        }

        public static implicit operator bool(JValue value) {
            return value is JBoolean jbool ? jbool.Value : false;
        }

        public static implicit operator int(JValue value) {
            return value is JNumber jnum ? (int)jnum.Value : 0;
        }

        // public bool Exists => !(this is JNull);

        /// <summary> Takes a path, returns true if a value exists there and passes it via the out param </summary>
        /// <param name="value"></param>
        /// <param name="locationPath"></param>
        /// <returns></returns>
        public bool TryGet(out JValue? value, params string[] locationPath) {
            try {
                int pos = 0;
                var obj = this;
                while (pos < locationPath.Length) {
                    var next = locationPath[pos];

                    if (int.TryParse(next, out int ind)) {
                        obj = obj[ind];
                    }
                    else {
                        obj = obj[next];
                    }

                    ++pos;
                }

                value = obj;
                return true;
            }
            catch {
                value = null;
                return false;
            }
        }

        public T? As<T>() {
            // Allow for casting to between J types
            if (typeof(T).BaseType == typeof(JValue)) {
                return (T)(object)this;
            }

            var targetType = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var actualType = nullableType ?? targetType;

            // Handle null values
            if (this is JNull) {
                // Maybe should throw :|
                return default(T);
            }

            // Handle arrays
            if (actualType.IsArray && this is JArray arr) {
                var elementType = actualType.GetElementType();
                var array = Array.CreateInstance(elementType!, arr.Elements.Count);

                // 1) Fill out array. This does NOT support mixed Array types, which are actually viable json
                for (int i = 0; i < arr.Elements.Count; i++) {
                    var element = arr.Elements[i].As(elementType!);
                    array.SetValue(element, i);
                }

                return (T)(object)array;
            }

            // Handle List<T>
            if (actualType.IsGenericType &&
                actualType.GetGenericTypeDefinition() == typeof(List<>) &&
                this is JArray listArr) {
                //ref:1
                var elementType = actualType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");

                foreach (var item in listArr.Elements) {
                    var element = item.As(elementType);
                    addMethod!.Invoke(list, new[] { element });
                }

                return (T)list!;
            }

            // Handle Dictionary<string, T>
            if (actualType.IsGenericType &&
                actualType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                actualType.GetGenericArguments()[0] == typeof(string) &&
                this is JObject obj) {
                //ref:1
                var valueType = actualType.GetGenericArguments()[1];
                var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                var dict = Activator.CreateInstance(dictType);
                var addMethod = dictType.GetMethod("Add");

                foreach (var kvp in obj.Fields) {
                    var value = kvp.Value.As(valueType);
                    addMethod!.Invoke(dict, new object[] { kvp.Key, value! });
                }

                return (T)dict!;
            }

            // Handle primitives
            return actualType.Name switch {
                nameof(String) when this is JString jstr => (T)(object)jstr.Value,
                nameof(Double) when this is JNumber jnum => (T)(object)jnum.Value,
                nameof(Single) when this is JNumber jnum => (T)(object)(float)jnum.Value,
                nameof(Int32) when this is JNumber jnum => (T)(object)(int)jnum.Value,
                nameof(Int64) when this is JNumber jnum => (T)(object)(long)jnum.Value,
                nameof(Boolean) when this is JBoolean jbool => (T)(object)jbool.Value,
                nameof(Decimal) when this is JNumber jnum => (T)(object)(decimal)jnum.Value,
                _ => default(T)
            };
        }

        // Helper method for non-generic casting (used internally)
        private object? As(Type targetType) {
            // Reflection at runtime is sus
            var method = typeof(JValue).GetMethod(nameof(As))!.MakeGenericMethod(targetType);
            return method.Invoke(this, null);
        }
    }


    public class JString : JValue {
        public string Value;

        public JString(string value) {
            Value = value;
        }

        public override string ToString() => $"\"{Value}\"";
    }

    public class JNumber : JValue {
        public double Value;

        public JNumber(double value) {
            Value = value;
        }

        public override string ToString() => $"{Value.ToString()}";
    }

    public class JBoolean : JValue {
        public bool Value;

        public JBoolean(bool value) {
            Value = value;
        }

        public override string ToString() => $"{Value.ToString().ToLower()}";
    }

    public class JNull : JValue {
        public override string ToString() => "null";
    }

    public class JObject : JValue {
        public Dictionary<string, JValue> Fields;

        public JValue this[string ind] {
            get => Fields[ind];
            set => Fields[ind] = value;
        }

        public JObject() {
            Fields = new();
        }

        public void Add(string key, JValue value) {
            Fields.Add(key, value);
        }

        public override string ToString() {
            var pairs = Fields.Select(x => $"\"{x.Key}\": {x.Value}");
            return $"{{{string.Join(", ", pairs)}}}";
        }
    }

    public class JArray : JValue {
        public List<JValue> Elements;

        public JValue this[int ind] {
            get => Elements[ind];
            set => Elements[ind] = value;
        }

        public JArray() {
            Elements = new();
        }

        public override string ToString() {
            return $"[{String.Join(", ", Elements)}]";
        }
    }


    public class Parser {
        private readonly List<JToken> _tokens;
        private int _position = 0;
        private Settings _settings;

        public Parser(List<JToken> tokens, Settings? settings = null) {
            _tokens = tokens;

            if (settings == null) settings = new();
            else this._settings = settings.Value;
        }

        public struct Settings {
            public bool AllowTrailingCommas = false;

            public Settings() { }
        }

        private JToken? Current => _position < _tokens.Count ? _tokens[_position] : null;
        private JToken? Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : null;

        // Eat a token if its a certain type, otherwise throw
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

        // The functon to call to parse your json
        public JValue Parse() {
            var result = ParseValue();
            if (_position < _tokens.Count) {
                throw new Exception($"Parsing did not complete: {Current} {Current?.Pos()}");
            }

            return result;
        }

        // Called recursively within parsenull, parseobject, parsearray
        private JValue ParseValue() {
            if (Current == null) {
                throw new Exception($"Unexpected end of stream {Current?.Pos()}");
            }

            return Current.Type switch {
                JTokenType.Word => new JString(ConsumeAny().Value),
                JTokenType.Num => new JNumber(double.Parse(ConsumeAny().Value)),
                JTokenType.Boolean => new JBoolean(bool.Parse(ConsumeAny().Value)),
                JTokenType.Null => ParseNull(),
                JTokenType.CurlOp => ParseObject(),
                JTokenType.SqOp => ParseArray(),
                _ => throw new Exception($"Unexpected token {Current} {Current?.Pos()}")
            };
        }

        private JValue ParseNull() {
            // lambdas not allowed in switch :(((((
            ConsumeAny();
            return new JNull();
        }

        private JObject ParseObject() {
            Consume(JTokenType.CurlOp);
            var obj = new JObject();

            // Empty object
            if (Current?.Type == JTokenType.CurlCl) {
                Consume(JTokenType.CurlCl);
                return obj;
            }

            while (true) {
                if (Current?.Type == JTokenType.CurlCl) {
                    if (_settings.AllowTrailingCommas) {
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

            return obj;
        }

        private JArray ParseArray() {
            // [
            Consume(JTokenType.SqOp);
            var arr = new JArray();

            while (true) {
                if (Current?.Type == JTokenType.SqCl) {
                    if (_settings.AllowTrailingCommas) {
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

                throw new Exception($"Expected `,` or `}}}}` but got {Current} {Current?.Pos()}");
            }

            return arr;
        }
    }

    // TODO support empty arrays
    // TODO support comments
    // TOOD Row and Col are really rough

    static void Main(string[] args) {
        Stopwatch sw = new();
        sw.Start();

        var content = File.ReadAllText("TestFile.json");
        var tokens = Lex(content);

        var parser = new Parser(tokens);
        var jsonVal = parser.Parse() as JObject;

        sw.Stop();
        tokens.ForEach(x => Console.Write($"{x}\n"));
        Console.WriteLine($"Lexing and parsing took {sw.ElapsedMilliseconds} ms");

        if (jsonVal.TryGet(out var v, "rootObject", "properties", "references")) {
            v.As<JArray>().Elements.Add(new JString("REF-004"));
            Console.WriteLine(String.Join(", ", v.As<List<string>>()));
        }

        // Console.WriteLine(jsonVal.ToString());
    }
}