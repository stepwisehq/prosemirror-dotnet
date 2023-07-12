using System.Text.Json.Serialization;


namespace StepWise.Prose.Model;

public class ReplaceException : Exception {
    public ReplaceException() {}
    public ReplaceException(string message) : base(message) {}
    public ReplaceException(string message, Exception innerException) : base(message, innerException) {}
}

public class Slice {
    public Fragment Content { get; init; }
    public int OpenStart { get; init; }
    public int OpenEnd { get; init; }

    public Slice(Fragment content, int openStart, int openEnd) {
        Content = content;
        OpenStart = openStart;
        OpenEnd = openEnd;
    }

    public int Size => Content.Size - OpenStart - OpenEnd;

    public Slice? InsertAt(int pos, Fragment fragment) {
        var content = Content.InsertInto(pos + OpenStart, fragment);
        return content is null ? null : new Slice(content, OpenStart, OpenEnd);
    }

    public Slice RemoveBetween(int from, int to) {
        return new Slice(Content.RemoveRange(from + OpenStart, to + OpenStart), OpenStart, OpenEnd);
    }

    public bool Eq(Slice other) {
        return Content.Eq(other.Content) && OpenStart == other.OpenStart && OpenEnd == other.OpenEnd;
    }

    public override string ToString() {
        return $"{Content}({OpenStart},{OpenEnd})";
    }

    public SliceDto? ToJSON() {
        var content = Content.ToJSON();
        if (content is null) return null;
        return new() {
            Content = content,
            OpenStart = OpenStart,
            OpenEnd = OpenEnd,
        };
    }

    public static Slice FromJSON(Schema schema, SliceDto? json) {
        if (json is null) return Slice.Empty;
        var openStart = json.OpenStart;
        var openEnd = json.OpenEnd;
        return new(Fragment.FromJSON(schema, json.Content), openStart, openEnd);
    }

    public static Slice MaxOpen(Fragment fragment, bool openIsolating = true) {
        int openStart = 0, openEnd = 0;
        for (var n = fragment.FirstChild; n is not null && !n.IsLeaf && (openIsolating || !(n.Type.Spec.Isolating ?? false)); n = n.FirstChild) openStart++;
        for (var n = fragment.LastChild; n is not null && !n.IsLeaf && (openIsolating || !(n.Type.Spec.Isolating ?? false)); n = n.LastChild) openEnd++;
        return new Slice(fragment, openStart, openEnd);
    }

    public static Slice Empty { get; } = new Slice(Fragment.Empty, 0, 0);
}


public static class FragmentExtensions {
    public static Fragment RemoveRange(this Fragment content, int from, int to) {
        var (index, offset) = content.FindIndex(from);
        var child = content.MaybeChild(index);
        var (indexTo, offsetTo) = content.FindIndex(to);
        if (offset == from || child!.IsText) {
            if (offsetTo != to && !content.Child(indexTo).IsText) throw new Exception("Removing non-flat range");
            return content.Cut(0, from).Append(content.Cut(to));
        }
        if (index != indexTo) throw new Exception("Removing non-flat range");
        return content.ReplaceChild(index, child!.Copy(child!.Content.RemoveRange(from - offset - 1, to - offset - 1)));
    }

    public static Fragment? InsertInto(this Fragment content, int dist, Fragment insert, Node? parent = null) {
        var (index, offset) = content.FindIndex(dist);
        var child = content.MaybeChild(index);
        if (offset == dist || child!.IsText) {
            if (parent is not null && !parent.CanReplace(index, index, insert)) return null;
            return content.Cut(0, dist).Append(insert).Append(content.Cut(dist));
        }
        var inner = child!.Content.InsertInto(dist - offset - 1, insert);
        return inner is null ? null : content.ReplaceChild(index, child!.Copy(inner));
    }
}

public static class ReplaceUtils {
    public static Node Replace(ResolvedPos from, ResolvedPos to, Slice slice) {
        if (slice.OpenStart > from.Depth) throw new Exception("Inserted content deeper than insertion position");
        if (from.Depth - slice.OpenStart != to.Depth - slice.OpenEnd) throw new Exception("Inconsistent open depths");
        return ReplaceOuter(from, to, slice, 0);
    }

    public static Node ReplaceOuter(ResolvedPos from, ResolvedPos to, Slice slice, int depth) {
        var index = from.Index(depth);
        var node = from.Node(depth);
        if (index == to.Index(depth) && depth < from.Depth - slice.OpenStart) {
            var inner = ReplaceOuter(from, to, slice, depth + 1);
            return node.Copy(node.Content.ReplaceChild(index, inner));
        } else if (slice.Content.Size == 0) {
            return Close(node, ReplaceTwoWay(from, to, depth));
        } else if (slice.OpenStart == 0 && slice.OpenEnd == 0 && from.Depth == depth && to.Depth == depth) {
            var parent = from.Parent;
            var content = parent.Content;
            return Close(parent, content.Cut(0, from.ParentOffset).Append(slice.Content).Append(content.Cut(to.ParentOffset)));
        } else {
            var (start, end) = PrepareSliceForReplace(slice, from);
            return Close(node, ReplaceThreeWay(from, start, end, to, depth));
        }
    }

    public static void CheckJoin(Node main, Node sub) {
        if (!sub.Type.CompatibleContent(main.Type))
            throw new ReplaceException($"Cannot join {sub.Type.Name} onto {main.Type.Name}");
    }

    public static Node Joinable(ResolvedPos before, ResolvedPos after, int depth) {
        var node = before.Node(depth);
        CheckJoin(node, after.Node(depth));
        return node;
    }

    public static void AddNode(Node child, List<Node> target) {
        var last = target.Count - 1;
        if (last >= 0 && child.IsText && child is TextNode textChild && child.SameMarkup(target[last]))
            target[last] = textChild.WithText(target[last].Text + textChild.Text);
        else
            target.Add(child);
    }

    public static void AddRange(ResolvedPos? start, ResolvedPos? end, int depth, List<Node> target) {
        var node = (end ?? start!).Node(depth);
        var startIndex = 0;
        var endIndex = end?.Index(depth) ?? node.ChildCount;
        if (start is not null) {
            startIndex = start.Index(depth);
            if (start.Depth > depth) {
                startIndex++;
            } else if (start.TextOffset > 0) {
                AddNode(start.NodeAfter!, target);
                startIndex++;
            }
        }
        for (var i = startIndex; i < endIndex; i++) AddNode(node.Child(i), target);
        if (end is not null && end.Depth == depth && end.TextOffset > 0)
            AddNode(end.NodeBefore!, target);
    }


    public static Node Close(Node node, Fragment content) {
        node.Type.CheckContent(content);
        return node.Copy(content);
    }

    public static Fragment ReplaceThreeWay(ResolvedPos from, ResolvedPos start, ResolvedPos end, ResolvedPos to, int depth) {
        var openStart = from.Depth > depth ? Joinable(from, start, depth + 1) : null;
        var openEnd = to.Depth > depth ? Joinable(end, to, depth + 1) : null;

        var content = new List<Node>();
        AddRange(null, from, depth, content);
        if (openStart is not null && openEnd is not null && start.Index(depth) == end.Index(depth)) {
            CheckJoin(openStart, openEnd);
            AddNode(Close(openStart, ReplaceThreeWay(from, start, end, to, depth + 1)), content);
        } else {
            if (openStart is not null)
                AddNode(Close(openStart, ReplaceTwoWay(from, start, depth + 1)), content);
            AddRange(start, end, depth, content);
            if (openEnd is not null)
                AddNode(Close(openEnd, ReplaceTwoWay(end, to, depth + 1)), content);
        }
        AddRange(to, null, depth, content);
        return new Fragment(content);
    }

    public static Fragment ReplaceTwoWay(ResolvedPos from, ResolvedPos to, int depth) {
        var content = new List<Node>();
        AddRange(null, from, depth, content);
        if (from.Depth > depth) {
            var type = Joinable(from, to, depth + 1);
            AddNode(Close(type, ReplaceTwoWay(from, to, depth +1)), content);
        }
        AddRange(to, null, depth, content);
        return new Fragment(content);
    }

    public static (ResolvedPos, ResolvedPos) PrepareSliceForReplace(Slice slice, ResolvedPos along) {
        var extra = along.Depth - slice.OpenStart;
        var parent = along.Node(extra);
        var node = parent.Copy(slice.Content);
        for (var i = extra - 1; i >= 0; i--)
            node = along.Node(i).Copy(Fragment.From(node));
        return (node.ResolveNoCache(slice.OpenStart + extra), node.ResolveNoCache(node.Content.Size - slice.OpenEnd - extra));
    }
}

public record SliceDto {
    public required List<NodeDto> Content { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int OpenStart { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int OpenEnd { get; init; }
}