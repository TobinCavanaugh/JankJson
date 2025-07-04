using System.Collections;
using System.Globalization;

namespace JJson;

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

    public bool IsLeaf => IsNumber | IsBoolean | IsString | IsNull;
    public bool IsNode => IsArray | IsObject;

    public List<JValue> Children => IsArray
        ? (this.AsJArray().Elements)
        : (this.IsObject
            ? (this.AsJObject().Fields.Select(x => x.Value).ToList())
            : throw new TypeAccessException($"This JValue is not a JArray or JObject, so it has no Children.\n" +
                                            $"IsArray: {IsArray}, IsObject: {IsObject}, IsString: {IsString}, IsNumber: {IsNumber}, IsBoolean: {IsBoolean}, IsNull: {IsNull}\n" +
                                            $"RawContent: `{this.RawContent}`"));

    public string ValueToString() {
        if (IsNumber) {
            return this.AsNumber().ToString(CultureInfo.InvariantCulture);
        }

        if (IsBoolean) {
            return this.AsBoolean().ToString();
        }

        if (IsString) {
            return this.AsString();
        }

        if (IsNull) {
            return "null";
        }

        throw new NotImplementedException("Nodes do not have values");
        return "<UNSET>";
    }

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