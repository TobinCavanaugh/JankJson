using System.Collections;

namespace parseJson;

//TODO REGULATE STRING ERRORS

public abstract class JValue {
    // TODO Make it include string content optionally
    public string? RawContent = null;
    public JValue? Parent = null;

    public abstract override string ToString();

    // Add convenience properties for type checking
    public bool IsArray => this is JArray;
    public bool IsObject => this is JObject;
    public bool IsString => this is JString;
    public bool IsNumber => this is JNumber;
    public bool IsBoolean => this is JBoolean;
    public bool IsNull => this is JNull;


    public virtual JValue this[string key] {
        get {
            if (this is JObject obj && obj.Fields.ContainsKey(key)) {
                return obj.Fields[key];
            }

            throw new Exception($"String indexer `{key}` called on non JObject type: `{this.GetType()}`");
        }
    }

    public virtual JValue this[int index] {
        get {
            if (this is JArray arr && Math.Clamp(index, 0, arr.Elements.Count - 1) == index) {
                return arr.Elements[index];
            }

            throw new Exception($"String indexer called on non JArray type: `{this.GetType()}`");
        }
    }

    public static explicit operator string(JValue value) {
        return value.As<string>() ?? "";
    }

    public static explicit operator double(JValue value) {
        return value is JNumber jnum ? jnum.Value : 0.0;
    }

    public static explicit operator bool(JValue value) {
        return value is JBoolean jbool ? jbool.Value : false;
    }

    public static explicit operator int(JValue value) {
        return value is JNumber jnum ? (int)jnum.Value : 0;
    }

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
            return default(T);
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
        var method = typeof(JValue).GetMethod(nameof(As))!.MakeGenericMethod(targetType);
        return method.Invoke(this, null);
    }
}

public class JString : JValue {
    public string Value;

    public JString(string value) {
        Value = value;
    }

    public override string ToString() => $"\"{StringExt.EscapeString(Value)}\"";
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

// Make JObject enumerable over its values
public class JObject : JValue, IEnumerable<JValue> {
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

    // IEnumerable implementation - iterate over values
    public IEnumerator<JValue> GetEnumerator() {
        return Fields.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    // Additional enumerable options
    public IEnumerable<KeyValuePair<string, JValue>> Pairs => Fields;
    public IEnumerable<string> Keys => Fields.Keys;
    public IEnumerable<JValue> Values => Fields.Values;
}

// Make JArray enumerable
public class JArray : JValue, IEnumerable<JValue> {
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

    // IEnumerable implementation
    public IEnumerator<JValue> GetEnumerator() {
        return Elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}

public static class JValueExtensions {
    private static string GetHelpfulRef(this JValue value) {
        return $"{value.RawContent}";
    }

    public static List<T> AsList<T>(this JValue value) {
        var l = value.AsJArray().Elements.Select(x => x.As<T>()).ToList();
        return l;
    }

    public static T[] AsArray<T>(this JValue value) {
        var l = value.AsJArray().Elements.Select(x => x.As<T>()).ToArray();
        return l;
    }

    public static JArray AsJArray(this JValue value) {
        return value as JArray ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not an array");
    }

    public static JObject AsJObject(this JValue value) {
        return value as JObject ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not an object");
    }

    public static double AsNumber(this JValue value) {
        var n = value as JNumber ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not a number");
        return n.Value;
    }

    public static string AsString(this JValue value) {
        var a = value as JString ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not a string");
        return a.Value;
    }

    public static bool AsBoolean(this JValue value) {
        var b = value as JBoolean ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not a boolean");
        return b.Value;
    }

    public static JNull AsJNull(this JValue value) {
        return value as JNull ?? throw new Exception($"Object `{value.GetHelpfulRef()}` is not a null");
    }


    // Recursive descendant traversal for deep LINQ queries
    public static IEnumerable<JValue> Descendants(this JValue value) {
        if (value is JObject obj) {
            foreach (var field in obj.Fields.Values) {
                yield return field;
                foreach (var descendant in field.Descendants()) {
                    yield return descendant;
                }
            }
        }
        else if (value is JArray arr) {
            foreach (var element in arr.Elements) {
                yield return element;
                foreach (var descendant in element.Descendants()) {
                    yield return descendant;
                }
            }
        }
    }

    // Get all descendants of a specific type
    public static IEnumerable<T> DescendantsOfType<T>(this JValue value) where T : JValue {
        return value.Descendants().OfType<T>();
    }
}