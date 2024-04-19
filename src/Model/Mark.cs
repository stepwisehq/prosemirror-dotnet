using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using StepWise.Prose.Collections;


namespace StepWise.Prose.Model;

using OptionalAttrs = DotNext.Optional<Dictionary<string, DotNext.Optional<JsonNode>>>;

public class MarkList : List<Mark> {}

public class Mark {
    public MarkType Type { get; init; }
    public Attrs Attrs { get; init; }

    public Mark(MarkType type, Attrs attrs) {
        Type = type;
        Attrs = attrs;
    }

    public List<Mark> AddToSet(List<Mark> set) {
        List<Mark>? copy = null;
        var placed = false;
        for (var i = 0; i < set.Count; i++) {
            var other = set[i];
            if (Eq(other)) return set;
            if (Type.Excludes(other.Type)) {
                copy ??= set.slice(0, i);
            } else if (other.Type.Excludes(Type)) {
                return set;
            } else {
                if (!placed && other.Type.Rank > Type.Rank) {
                    copy ??= set.slice(0, i);
                    copy.Add(this);
                    placed = true;
                }
                copy?.Add(other);
            }
        }
        copy ??= set.ToList();
        if (!placed) copy.Add(this);
        return copy;
    }

    public List<Mark> RemoveFromSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++) {
            if (Eq(set[i])) {
                var copy = set.slice(0, i);
                copy.AddRange(set.slice(i + 1));
                return copy;
            }
        }
        return set;
    }

    public bool IsInSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++)
            if (Eq(set[i])) return true;
        return false;
    }

    public bool Eq(Mark other) {
        return ReferenceEquals(this, other) ||
            (ReferenceEquals(Type, other.Type) && Attrs.CompareDeep(Attrs, other.Attrs));
    }

    public MarkDto ToJSON() {
        var attrs = Attrs.Count == 0 ? null : new Dictionary<string, DotNext.Optional<JsonNode>>();
        if (attrs is not null) foreach (var (name, value) in Attrs) attrs[name] = value;

        return new() {
            Type = Type.Name,
            Attrs = attrs is not null ? attrs : OptionalAttrs.None
        };
    }

    public static Mark FromJSON(Schema schema, MarkDto json) {
        if (!schema.Marks.TryGetValue(json.Type, out var type)) throw new Exception($"There is no mark type {json.Type}");

        var jsonAttrs = json.Attrs.HasValue ? json.Attrs.Value : new Dictionary<string, DotNext.Optional<JsonNode>>();
        var attrs = new Attrs();
        foreach (var (name, value) in jsonAttrs) attrs[name] = value.IsNull ? null : value.Value;
        return type.Create(attrs);
    }

    public static bool SameSet(List<Mark> a, List<Mark> b) {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!a[i].Eq(b[i])) return false;
        return true;
    }

    public static List<Mark> SetFrom() => None;
    public static List<Mark> SetFrom(Mark? mark) {
        if (mark is null) return None;
        return new() {mark};
    }
    public static List<Mark> SetFrom(List<Mark>? marks) {
        if (marks is null || marks.Count == 0) return None;
        var copy = marks.ToList();
        copy.Sort((a, b) => a.Type.Rank - b.Type.Rank);
        return copy;
    }

    public static string WrapMarks(List<Mark> marks, string str) {
        for (var i = marks.Count - 1; i >= 0; i--)
            str = $"{marks[i].Type.Name}({str})";
        return str;
    }

    public static List<Mark> None { get; } = new();
}

public record MarkDto {
    public required string Type { get; init; }
    // Use of optional dictionary values here is to work around a bug in deserializing null JsonNodes
    // https://github.com/dotnet/runtime/issues/85172
    // Wasn't backported to Dotnet 7 so can remove when DotNet 8 is released.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalAttrs Attrs { get; init; }
}