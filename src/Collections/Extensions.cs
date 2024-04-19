namespace StepWise.Prose.Collections;

public static class ListExtensions {
    public static List<T> slice<T>(this List<T> list) =>
        list.ToList();

    public static List<T> slice<T>(this List<T> list, int start) =>
        list.GetRange(start, list.Count - start);

    public static List<T> slice<T>(this List<T> list, int start, int end) =>
        list.GetRange(start, Math.Min(end - start, list.Count - start));

    public static string slice(this string str) =>
        str;

    public static string slice(this string str, int start) =>
        str[start..];

    public static string slice(this string str, int start, int end) =>
        str[start..Math.Min(end, str.Length)];

    public static T pop<T>(this List<T> list) {
        var item = list[^1];
        list.RemoveAt(list.Count - 1);
        return item;
    }
}