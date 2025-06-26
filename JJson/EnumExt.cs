namespace JJson;

public static class EnumExt {
    public static string StringJoin<T>(this IEnumerable<T> arr, string separator = ",") {
        return string.Join(separator, arr);
    }
}