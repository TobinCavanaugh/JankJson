
namespace JJson;

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

// Add this to your JValueExtensions class

    /// <summary>
    /// Recursively traverses the JSON structure, applying a function to each node
    /// </summary>
    /// <param name="action">Function to apply to each JValue. Return true to continue recursion into children, false to skip.</param>
    public static void Recurse(this JValue value, Func<JValue, bool> action) {
        if (!action(value)) return;

        if (value is JObject obj) {
            foreach (var field in obj.Fields.Values) {
                field.Recurse(action);
            }
        }
        else if (value is JArray arr) {
            foreach (var element in arr.Elements) {
                element.Recurse(action);
            }
        }
    }

    /// <summary>
    /// Recursively traverses the JSON structure, applying a function to each node with path context
    /// </summary>
    /// <param name="action">Function to apply to each JValue with its path. Return true to continue recursion, false to skip.</param>
    public static void Recurse(this JValue value, Func<JValue, string[], bool> action) {
        RecurseInternal(value, action, new string[0]);
    }

    private static void RecurseInternal(JValue value, Func<JValue, string[], bool> action, string[] currentPath) {
        if (!action(value, currentPath)) return;

        if (value is JObject obj) {
            foreach (var kvp in obj.Fields) {
                var newPath = currentPath.Concat(new[] { kvp.Key }).ToArray();
                RecurseInternal(kvp.Value, action, newPath);
            }
        }
        else if (value is JArray arr) {
            for (int i = 0; i < arr.Elements.Count; i++) {
                var newPath = currentPath.Concat(new[] { i.ToString() }).ToArray();
                RecurseInternal(arr.Elements[i], action, newPath);
            }
        }
    }

    /// <summary>
    /// Recursively traverses and transforms the JSON structure
    /// </summary>
    /// <param name="transform">Function that transforms each JValue</param>
    /// <returns>New JValue with transformations applied</returns>
    public static JValue RecurseTransform(this JValue value, Func<JValue, JValue> transform) {
        var transformed = transform(value);

        if (transformed is JObject obj) {
            var newObj = new JObject();
            foreach (var kvp in obj.Fields) {
                newObj.Fields[kvp.Key] = kvp.Value.RecurseTransform(transform);
            }

            return newObj;
        }
        else if (transformed is JArray arr) {
            var newArr = new JArray();
            foreach (var element in arr.Elements) {
                newArr.Elements.Add(element.RecurseTransform(transform));
            }

            return newArr;
        }

        return transformed;
    }

    /// <summary>
    /// Recursively collects values that match a predicate
    /// </summary>
    /// <param name="predicate">Function to test each JValue</param>
    /// <returns>Collection of matching JValues</returns>
    public static IEnumerable<JValue> RecurseWhere(this JValue value, Func<JValue, bool> predicate) {
        var results = new List<JValue>();

        value.Recurse(v => {
            if (predicate(v))
                results.Add(v);
            return true; // Always continue recursion
        });

        return results;
    }

    /// <summary>
    /// Recursively finds the first value that matches a predicate
    /// </summary>
    /// <param name="predicate">Function to test each JValue</param>
    /// <returns>First matching JValue or null if none found</returns>
    public static JValue? RecurseFirst(this JValue value, Func<JValue, bool> predicate) {
        JValue? found = null;

        value.Recurse(v => {
            if (predicate(v)) {
                found = v;
                return false; // Stop recursion
            }

            return true; // Continue recursion
        });

        return found;
    }
}