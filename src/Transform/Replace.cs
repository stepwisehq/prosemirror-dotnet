using StepWise.Prose.Collections;
using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public static class Replace {
    public static Step? replaceStep(Node doc, int from, int? to = null, Slice? slice = null) {
        to ??= from;
        slice ??= Slice.Empty;

        if (from == to && slice.Size == 0) return null;

        var _from = doc.Resolve(from);
        var _to = doc.Resolve(to.Value);
        if (FitsTrivially(_from, _to, slice)) return new ReplaceStep(from, to.Value, slice);
        return new Fitter(_from, _to, slice).Fit();
    }

    private static bool FitsTrivially(ResolvedPos from, ResolvedPos to, Slice slice) {
        return slice.OpenStart == 0 && slice.OpenEnd == 0 && from.Start() == to.Start() &&
            from.Parent.CanReplace(from.Index(), to.Index(), slice.Content);
    }

    private static bool DefinesContent(NodeType type) =>
        (type.Spec.Defining ?? false) || (type.Spec.DefiningForContext ?? false);

    public static Transform? ReplaceRange(Transform tr, int from, int to, Slice slice) {
        if (slice.Size == 0) return tr.Delete(from, to);

        ResolvedPos _from = tr.Doc.Resolve(from), _to = tr.Doc.Resolve(to);
        if (FitsTrivially(_from, _to, slice))
            return tr.Step(new ReplaceStep(from, to, slice));

        var targetDepths = CoveredDepths(_from, tr.Doc.Resolve(to));

        if (targetDepths.Count > 0 && targetDepths[^1] == 0) targetDepths.RemoveAt(targetDepths.Count - 1);

        var preferredTarget = -(_from.Depth + 1);
        targetDepths.Insert(0, preferredTarget);

        var pos = _from.Pos - 1;
        for (var d = _from.Depth; d > 0; d--, pos--) {
            var spec = _from.Node(d).Type.Spec;
            if ((spec.Defining ?? false) || (spec.DefiningAsContext ?? false) || (spec.Isolating ?? false)) break;
            if (targetDepths.Contains(d)) preferredTarget = d;
            else if (_from.Before(d) == pos) targetDepths.Insert(1, -d);
        }

        var preferredTargetIndex = targetDepths.IndexOf(preferredTarget);

        var leftNodes = new List<Node>();
        var preferredDepth = slice.OpenStart;
        var content = slice.Content;
        for (var i = 0;; i++) {
            var node = content.FirstChild!;
            leftNodes.Add(node);
            if (i == slice.OpenStart) break;
            content = node.Content;
        }

        for (var d = preferredDepth - 1; d >= 0; d--) {
            var type = leftNodes[d].Type;
            var def = DefinesContent(type);
            if (def && !ReferenceEquals(_from.Node(preferredTargetIndex).Type, type)) preferredDepth = d;
            else if (def || !type.IsTextBlock) break;
        }

        for (var j = slice.OpenStart; j >= 0; j--) {
            var openDepth = (j + preferredDepth + 1) % (slice.OpenStart + 1);
            var insert = leftNodes.ElementAtOrDefault(openDepth);
            if (insert is null) continue;
            for (var i = 0; i < targetDepths.Count; i++) {
                var targetDepth = targetDepths[(i + preferredTargetIndex) % targetDepths.Count];
                var expand = true;
                if (targetDepth < 0) { expand = false; targetDepth = -targetDepth; }
                var parent = _from.Node(targetDepth - 1);
                var index = _from.Index(targetDepth - 1);
                if (parent.CanReplaceWith(index, index, insert.Type, insert.Marks))
                    return tr.Replace(_from.Before(targetDepth), expand ? _to.After(targetDepth) : to,
                                      new Slice(CloseFragment(slice.Content, 0, slice.OpenStart, openDepth),
                                      openDepth, slice.OpenEnd));
            }
        }

        var startSteps = tr.Steps.Count;
        for (var i = targetDepths.Count - 1; i >= 0; i--) {
            tr.Replace(from, to, slice);
            if (tr.Steps.Count > startSteps) break;
            var depth = targetDepths[i];
            if (depth < 0) continue;
            from = _from.Before(depth);
            to = _to.After(depth);
        }
        return tr;
    }

    private static Fragment CloseFragment(Fragment fragment, int depth, int oldOpen, int newOpen, Node? parent = null) {
        if (depth < oldOpen) {
            var first = fragment.FirstChild!;
            fragment = fragment.ReplaceChild(0, first.Copy(CloseFragment(first.Content, depth + 1, oldOpen, newOpen, first)));
        }
        if (depth > newOpen) {
            var match = parent!.ContentMatchAt(0);
            var start = match.FillBefore(fragment)!.Append(fragment);
            fragment = start.Append(match.MatchFragment(start)!.FillBefore(Fragment.Empty, true)!);
        }
        return fragment;
    }

    public static void ReplaceRangeWith(Transform tr, int from, int to, Node node) {
        if (!node.IsInline && from == to && tr.Doc.Resolve(from).Parent.Content.Size > 0) {
            var point = Structure.InsertPoint(tr.Doc, from, node.Type);
            if (point is not null) from = to = point.Value;
        }
        tr.ReplaceRange(from, to, new Slice(Fragment.From(node), 0, 0));
    }

    public static Transform? DeleteRange(Transform tr, int from, int to) {
        ResolvedPos _from = tr.Doc.Resolve(from), _to = tr.Doc.Resolve(to);
        var covered = CoveredDepths(_from, _to);
        for (var i = 0; i < covered.Count; i++) {
            var depth = covered[i];
            var last = i == covered.Count - 1;
            if ((last && depth == 0) || _from.Node(depth).Type.ContentMatch.ValidEnd)
                return tr.Delete(_from.Start(depth), _to.End(depth));
            if (depth > 0 && (last || _from.Node(depth - 1).CanReplace(_from.Index(depth - 1), _to.IndexAfter(depth - 1))))
                return tr.Delete(_from.Before(depth), _to.After(depth));
        }
        for (var d = 1; d <= _from.Depth && d <= _to.Depth; d++) {
            if (from - _from.Start(d) == _from.Depth - d && to > _from.End(d) && _to.End(d) - to != _to.Depth - d)
            return tr.Delete(_from.Before(d), to);
        }
        tr.Delete(from, to);
        return null;
    }

    private static List<int> CoveredDepths(ResolvedPos from, ResolvedPos to) {
        var result = new List<int>();
        var minDepth = Math.Min(from.Depth, to.Depth);
        for (var d = minDepth; d >= 0; d--) {
            var start = from.Start(d);
            if (start < from.Pos - (from.Depth - d) ||
                to.End(d) > to.Pos + (to.Depth - d) ||
                (from.Node(d).Type.Spec.Isolating ?? false) ||
                (to.Node(d).Type.Spec.Isolating ?? false)) break;
            if (start == to.Start(d) ||
                (d == from.Depth && d == to.Depth && from.Parent.InlineContent && to.Parent.InlineContent &&
                 d > 0 && to.Start(d - 1) == start - 1))
            {
                result.Add(d);
            }
        }
        return result;
    }
}


file record Fittable(
    int SliceDepth,
    int FrontierDepth,
    Node? Parent,
    Fragment? Inject = null,
    List<NodeType>? Wrap = null
);

public record FrontierEntry( NodeType Type, ContentMatch Match) {
    public ContentMatch Match { get; set; } = Match;
}

file class Fitter {
    public List<FrontierEntry> Frontier { get; } = new();
    public Fragment Placed { get; private set; } = Fragment.Empty;
    public ResolvedPos From { get; }
    public ResolvedPos To { get; }
    public Slice Unplaced { get; private set; }

    public Fitter(ResolvedPos from, ResolvedPos to, Slice unplaced) {
        From = from;
        To = to;
        Unplaced = unplaced;

        for (var i = 0; i<= from.Depth; i++) {
            var node = from.Node(i);
            Frontier.Add(
                new(node.Type,
                node.ContentMatchAt(from.IndexAfter(i)))
            );
        }

        for (var i = from.Depth; i > 0; i--)
            Placed = Fragment.From(from.Node(i).Copy(Placed));
    }

    public int Depth => Frontier.Count - 1;

    public Step? Fit() {
        while (Unplaced.Size > 0) {
            var fit = FindFittable();
            if (fit is not null) PlaceNodes(fit);
            else if (!OpenMore()) DropNode();
        }

        var moveInline = MustMoveInline();
        var placedSize = Placed.Size - Depth - From.Depth;
        var from = From;
        var to = Close(moveInline < 0 ? To : from.Doc.Resolve(moveInline));
        if (to is null) return null;

        var content = Placed;
        var openStart = from.Depth;
        var openEnd = to.Depth;
        while (openStart > 0 && openEnd > 0 && content.ChildCount == 1) {
            content = content.FirstChild!.Content;
            openStart--; openEnd--;
        }
        var slice = new Slice(content, openStart, openEnd);
        if (moveInline > -1)
            return new ReplaceAroundStep(from.Pos, moveInline, To.Pos, To.End(), slice, placedSize);
        if (slice.Size > 0 || from.Pos != To.Pos)
            return new ReplaceStep(from.Pos, to.Pos, slice);
        return null;
    }

    public Fittable? FindFittable() {
        var startDepth = Unplaced.OpenStart;
        var cur = Unplaced.Content;
        int d = 0, openEnd = Unplaced.OpenEnd;
        for (; d < startDepth; d++) {
            var node = cur.FirstChild!;
            if (cur.ChildCount > 1) openEnd = 0;
            if ((node.Type.Spec.Isolating ?? false) && openEnd <= d) {
                startDepth = d;
                break;
            }
            cur = node.Content;
        }

        for (var pass = 1; pass <= 2; pass++) {
            for (var sliceDepth = pass == 1 ? startDepth : Unplaced.OpenStart; sliceDepth >= 0; sliceDepth--) {
                Fragment? fragment = null;
                Node? parent = null;
                if (sliceDepth > 0) {
                    parent = ContentAt(Unplaced.Content, sliceDepth - 1).FirstChild;
                    fragment = parent!.Content;
                } else {
                    fragment = Unplaced.Content;
                }
                var first = fragment.FirstChild;
                for (var frontierDepth = Depth; frontierDepth >=0; frontierDepth--) {
                    var (type, match) = Frontier.ElementAt(frontierDepth);
                    List<NodeType>? wrap = null;
                    Fragment? inject = null;

                    if (pass == 1 && (first is not null ? match.MatchType(first.Type) is not null || (inject = match.FillBefore(Fragment.From(first), false)) is not null
                                      : parent is not null && type.CompatibleContent(parent.Type)))
                        return new(sliceDepth, frontierDepth, parent, inject);

                    else if (pass == 2 && first is not null && (wrap = match.FindWrapping(first.Type)) is not null)
                        return new(sliceDepth, frontierDepth, parent, null, wrap);

                    if (parent is not null && match.MatchType(parent.Type) is not null) break;
                }
            }
        }
        return null;
    }

    public bool OpenMore() {
        var content = Unplaced.Content;
        var openStart = Unplaced.OpenStart;
        var openEnd = Unplaced.OpenEnd;
        var inner = ContentAt(content, openStart);
        if (inner.ChildCount == 0 || inner.FirstChild!.IsLeaf) return false;
        Unplaced = new Slice(content, openStart + 1,
                             Math.Max(openEnd, inner.Size + openStart >= content.Size - openEnd ? openStart + 1 : 0));
        return true;
    }

    public bool DropNode() {
        var content = Unplaced.Content;
        var openStart = Unplaced.OpenStart;
        var openEnd = Unplaced.OpenEnd;
        var inner = ContentAt(content, openStart);
        if (inner.ChildCount <= 1 && openStart > 0) {
            var openAtEnd = content.Size - openStart <= openStart + inner.Size;
            Unplaced = new Slice(DropFromFragment(content, openStart - 1, 1), openStart - 1,
                                 openAtEnd ? openStart - 1 : openEnd);
        } else {
            Unplaced = new Slice(DropFromFragment(content, openStart, 1), openStart, openEnd);
        }
        return true;
    }

    public void PlaceNodes(Fittable fit) {
        var (sliceDepth, frontierDepth, parent, inject, wrap) = fit;

        while (Depth > frontierDepth) CloseFrontierNode();
        if (wrap is not null) for (var i = 0; i < wrap.Count; i++) OpenFrontierNode(wrap[i]);

        var slice = Unplaced;
        var fragment = parent is not null ? parent.Content : slice.Content;
        var openStart = slice.OpenStart - sliceDepth;
        var taken = 0;
        List<Node> add = new();
        var (type, match) = Frontier.ElementAt(frontierDepth);
        if (inject is not null) {
            for (var i = 0; i < inject.ChildCount; i++) add.Add(inject.Child(i));
            match = match.MatchFragment(inject)!;
        }

        var openEndCount = (fragment.Size + sliceDepth) - (slice.Content.Size - slice.OpenEnd);

        while (taken < fragment.ChildCount) {
            var next = fragment.Child(taken);
            var matches = match.MatchType(next.Type);
            if (matches is null) break;
            taken++;
            if (taken > 1 || openStart == 0 || next.Content.Size > 0) {
                match = matches;
                add.Add(CloseNodeStart(next.Mark(type.AllowedMarks(next.Marks)), taken == 1 ? openStart : 0,
                                       taken == fragment.ChildCount ? openEndCount : -1));
            }
        }
        var toEnd = taken == fragment.ChildCount;
        if (!toEnd) openEndCount = -1;

        Placed = AddToFragment(Placed, frontierDepth, Fragment.From(add));
        Frontier.ElementAt(frontierDepth).Match = match;

        if (toEnd && openEndCount < 0 && parent is not null && ReferenceEquals(parent.Type, Frontier.ElementAt(frontierDepth).Type) && Frontier.Count > 1)
            CloseFrontierNode();

        var cur = fragment;
        for (var i = 0; i < openEndCount; i++) {
            var node = cur.LastChild!;
            Frontier.Add(new(node.Type, node.ContentMatchAt(node.ChildCount)));
            cur = node.Content;
        }

        Unplaced = !toEnd ? new Slice(DropFromFragment(slice.Content, sliceDepth, taken), slice.OpenStart, slice.OpenEnd)
            : sliceDepth == 0 ? Slice.Empty
            : new Slice(DropFromFragment(slice.Content, sliceDepth - 1, 1),
                        sliceDepth - 1, openEndCount < 0 ? slice.OpenEnd : sliceDepth - 1);

    }

    public int MustMoveInline() {
        if (!To.Parent.IsTextBlock) return -1;
        var top = Frontier.ElementAt(Depth);
        CloseLevel? level = null;
        if (!top.Type.IsTextBlock || ContentAfterFits(To, To.Depth, top.Type, top.Match, false) is null ||
            (To.Depth == Depth && (level = FindCloseLevel(To)) is not null && level.Depth == Depth)) return -1;

        var depth = To.Depth;
        var after = To.After(depth);
        while (depth > 1 && after == To.End(--depth)) ++after;
        return after;
    }

    private record CloseLevel(int Depth, Fragment Fit, ResolvedPos Move);

    private CloseLevel? FindCloseLevel(ResolvedPos to) {
        for (var i = Math.Min(Depth, to.Depth); i >= 0; i--) {
            var (type, match) = Frontier.ElementAt(i);
            var dropInner = i < to.Depth && to.End(i + 1) == to.Pos + (to.Depth - (i + 1));
            var fit = ContentAfterFits(to, i, type, match, dropInner);
            if (fit is null) continue;
            for (var d = i - 1; d >= 0; d--) {
                var (_type, _match) = Frontier.ElementAt(d);
                var matches = ContentAfterFits(to, d, _type, _match, true);
                if (matches is null || matches.ChildCount > 0) goto scan;
            }
            return new(i, fit, dropInner ? to.Doc.Resolve(to.After(i + 1)) : to);
            scan: continue;
        }
        return null;
    }

    public ResolvedPos? Close(ResolvedPos to) {
        var close = FindCloseLevel(to);
        if (close is null) return null;

        while (Depth > close.Depth) CloseFrontierNode();
        if (close.Fit.ChildCount > 0) Placed = AddToFragment(Placed, close.Depth, close.Fit);
        to = close.Move;
        for (var d = close.Depth + 1; d <= to.Depth; d++) {
            var node = to.Node(d);
            var add = node.Type.ContentMatch.FillBefore(node.Content, true, to.Index(d));
            OpenFrontierNode(node.Type, node.Attrs, add);
        }
        return to;
    }

    public void OpenFrontierNode(NodeType type, Attrs? attrs = null, Fragment? content = null) {
        var top = Frontier.ElementAt(Depth);
        top.Match = top.Match.MatchType(type)!;
        Placed = AddToFragment(Placed, Depth, Fragment.From(type.Create(attrs, content)));
        Frontier.Add(new(type, type.ContentMatch));
    }

    public void CloseFrontierNode() {
        var open = Frontier.Pop();
        var add = open.Match.FillBefore(Fragment.Empty, true)!;
        if (add.ChildCount > 0) Placed = AddToFragment(Placed, Frontier.Count, add);

    }

    public static Fragment DropFromFragment(Fragment fragment, int depth, int count) {
        if (depth == 0) return fragment.CutByIndex(count, fragment.ChildCount);
        return fragment.ReplaceChild(0, fragment.FirstChild!.Copy(DropFromFragment(fragment.FirstChild!.Content, depth - 1, count)));
    }

    public static Fragment AddToFragment(Fragment fragment, int depth, Fragment content) {
        if (depth == 0) return fragment.Append(content);
        return fragment.ReplaceChild(fragment.ChildCount - 1,
                                     fragment.LastChild!.Copy(AddToFragment(fragment.LastChild!.Content, depth - 1, content)));
    }

    public static Fragment ContentAt(Fragment fragment, int depth) {
        for (var i = 0; i < depth; i++) fragment = fragment.FirstChild!.Content;
        return fragment;
    }

    public static Node CloseNodeStart(Node node, int openStart, int openEnd) {
        if (openStart <= 0) return node;
        var frag = node.Content;
        if (openStart > 1)
            frag = frag.ReplaceChild(0, CloseNodeStart(frag.FirstChild!, openStart - 1, frag.ChildCount == 1 ? openEnd - 1 : 0));
        if (openStart > 0) {
            frag = node.Type.ContentMatch.FillBefore(frag)!.Append(frag);
            if (openEnd <= 0) frag = frag.Append(node.Type.ContentMatch.MatchFragment(frag)!.FillBefore(Fragment.Empty, true)!);
        }
        return node.Copy(frag);
    }

    public static Fragment? ContentAfterFits(ResolvedPos to, int depth, NodeType type, ContentMatch match, bool open) {
        var node = to.Node(depth);
        var index = open ? to.IndexAfter(depth) : to.Index(depth);
        if (index == node.ChildCount && !type.CompatibleContent(node.Type)) return null;
        var fit = match.FillBefore(node.Content, true, index);
        return fit is not null && !InvalidMarks(type, node.Content, index) ? fit: null;
    }

    public static bool InvalidMarks(NodeType type, Fragment fragment, int start) {
        for (var i = start; i < fragment.ChildCount; i++)
            if (!type.AllowsMarks(fragment.Child(i).Marks)) return true;
        return false;
    }

    public static bool? DefinesContent(NodeType type) {
        return (type.Spec.Defining ?? false) || (type.Spec.DefiningForContext ?? false);
    }


}