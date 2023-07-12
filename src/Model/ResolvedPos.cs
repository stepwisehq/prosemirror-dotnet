using OneOf;


namespace StepWise.Prose.Model;

using PathList = List<OneOf<Node, int>>;

public class ResolvedPos {
    public static ResolveCache Cache { get; } = new();

    public int Pos { get; init; }
    public PathList Path { get; init; }
    public int ParentOffset { get; init; }

    public int Depth { get; init; }

    public ResolvedPos(
        int pos,
        PathList path,
        int parentOffset
    ) {
        Pos = pos;
        Path = path;
        ParentOffset = parentOffset;
        Depth =
        path.Count / 3 - 1;
    }

    public int ResolveDepth(int? val) {
        if (val is null) return Depth;
        if (val < 0) return Depth + val.Value;
        return val.Value;
    }

    public Node Parent => Node(Depth);

    public Node Doc => Node(0);

    public Node Node(int? depth) => (Node)Path[ResolveDepth(depth) * 3].Value;

    public int Index(int? depth = null) => (int)Path[ResolveDepth(depth) * 3 + 1].Value;

    public int IndexAfter(int? depth) {
        var _depth = ResolveDepth(depth);
        return Index(_depth) + (_depth == Depth && TextOffset == 0 ? 0 : 1 );
    }

    public int Start(int? depth = null) {
        var _depth = ResolveDepth(depth);
        return _depth == 0 ? 0 : (int)Path[_depth * 3 - 1].Value + 1;
    }

    public int End(int? depth = null) {
        var _depth = ResolveDepth(depth);
        return Start(_depth) + Node(_depth).Content.Size;
    }

    public int Before(int? depth) {
        int _depth;
        try {
            _depth = ResolveDepth(depth);
        } catch (ArgumentOutOfRangeException e) {
            throw new Exception("There is no position before the top-level node", e);
        }
        return _depth == Depth + 1 ? Pos : (int)Path[_depth * 3 - 1].Value;
    }

    public int After(int? depth) {
        int _depth;
        try {
            _depth = ResolveDepth(depth);
        } catch (ArgumentOutOfRangeException e) {
            throw new Exception("There is no position after the top-level node", e);
        }
        return _depth == Depth + 1 ? Pos : (int)Path[_depth * 3 - 1].Value + Node(depth).NodeSize;
    }

    public int TextOffset => Pos - (int)Path[^1].Value;

    public Node? NodeAfter { get {
        var parent = Parent;
        var index = Index(Depth);
        if (index == parent.ChildCount) return null;
        var dOff = Pos - (int)Path[^1].Value;
        var child = parent.Child(index);
        return dOff > 0 ? parent.Child(index).Cut(dOff) : child;
    }}

    public Node? NodeBefore { get {
        var index = Index(Depth);
        var dOff = Pos - (int)Path[^1].Value;
        if (dOff > 0) return Parent.Child(index).Cut(0, dOff);
        return index == 0 ? null : Parent.Child(index - 1);
    }}

    public int PosAtIndex(int index, int? depth = null) {
        var _depth = ResolveDepth(depth);
        var node = Node(depth);
        var pos = _depth == 0 ? 0 : (int)Path[_depth * 3 - 1].Value + 1;
        for (var i = 0; i < index; i++) pos += node.Child(i).NodeSize;
        return pos;
    }

    public List<Mark> Marks() {
        var parent = Parent;
        var index = Index();

        if (parent.Content.Size == 0) return Mark.None;

        if (TextOffset > 0) return parent.Child(index).Marks;

        var main = parent.MaybeChild(index - 1);
        var other = parent.MaybeChild(index);

        if (main is null) (other, main) = (main, other);

        var marks = main!.Marks;
        for (var i = 0; i < marks.Count; i++)
            if (marks[i].Type.Spec.Inclusive == false && (other is null || !marks[i].IsInSet(other.Marks)))
                marks = marks[i--].RemoveFromSet(marks);

        return marks;
    }

    public List<Mark>? MarksAcross(ResolvedPos end) {
        var after = Parent.MaybeChild(Index());
        if (after is null || !after.IsInline) return null;

        var marks = after.Marks;
        var next = end.Parent.MaybeChild(end.Index());
        for (var i = 0; i < marks.Count; i++)
            if (marks[i].Type.Spec.Inclusive == false && (next is null || !marks[i].IsInSet(next.Marks)))
                marks = marks[i--].RemoveFromSet(marks);

        return marks;
    }

    public int SharedDepth(int pos) {
        for (var depth = Depth; depth > 0; depth--)
            if (Start(depth) <= pos && End(depth) >= pos) return depth;
        return 0;
    }

    public NodeRange? BlockRange(ResolvedPos? other = null, Func<Node, bool>? pred = null) {
        other ??= this;
        if (other.Pos < Pos) return other.BlockRange(this);
        for (var d = Depth -(Parent.InlineContent || Pos == other.Pos ? 1 : 0); d >= 0; d--)
            if (other.Pos <= End(d) && (pred is null || pred(Node(d))))
                return new NodeRange(this, other, d);
        return null;
    }

    public bool SameParent(ResolvedPos other) {
        return Pos - ParentOffset == other.Pos - other.ParentOffset;
    }

    public ResolvedPos Max(ResolvedPos other) {
        return other.Pos > Pos ? other : this;
    }

    public ResolvedPos Min(ResolvedPos other) {
        return other.Pos < Pos ? other : this;
    }

    public override string ToString() {
        var str = "";
        for (var i = 1; i <= Depth; i++)
            str += (str != string.Empty ? "/" : "") + Node(i).Type.Name + "_" + Index(i - 1);
        return str + $":{ParentOffset}";
    }

    public static ResolvedPos Resolve(Node doc, int pos) {
        if (!(pos >= 0 && pos <= doc.Content.Size)) throw new Exception("Position " + pos + " out of range");
        var path = new PathList();
        int start = 0, parentOffset = pos;
        for (var node = doc;;) {
            var (index, offset) = node.Content.FindIndex(parentOffset);
            var rem = parentOffset - offset;
            path.Add(node); path.Add(index); path.Add(start + offset);
            if (rem <= 0) break;
            node = node.Child(index);
            if (node.IsText) break;
            parentOffset = rem - 1;
            start += offset + 1;
        }
        return new ResolvedPos(pos, path, parentOffset);
    }

    public static ResolvedPos ResolveCached(Node doc, int pos) {
        for (var i = 0; i < Cache.Cache.Length; i++) {
            var cached = Cache.Cache[i];
            if (cached is null) break;
            if (cached.Pos == pos && ReferenceEquals(cached.Doc, doc)) return cached;
        }
        var result = Resolve(doc, pos);
        Cache.Cache[Cache.Pos] = result;
        Cache.Pos = (Cache.Pos + 1) % Cache.Cache.Length;
        return result;
    }
}

public record ResolveCache {
    public ResolvedPos[] Cache { get; } = new ResolvedPos[12];
    public int Pos { get; set; } = 0;
}


public record NodeRange(ResolvedPos From, ResolvedPos To, int Depth) {
    public int Start => From.Before(Depth + 1);

    public int End => To.After(Depth + 1);

    public Node Parent => From.Node(Depth);

    public int StartIndex => From.Index(Depth);

    public int EndIndex => To.IndexAfter(Depth);
}