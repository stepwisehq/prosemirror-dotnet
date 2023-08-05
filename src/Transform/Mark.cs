using System.Text.RegularExpressions;

using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public static partial class Mark {
    [GeneratedRegex(@"\r?\n|\r")]
    private static partial Regex _NewLineRegex();

    public static void AddMark(Transform tr, int from, int to, Model.Mark mark) {
        List<Step> removed = new(), added = new();
        RemoveMarkStep? removing = null;
        AddMarkStep? adding = null;
        tr.Doc.NodesBetween(from, to, (node, pos, parent, _) => {
            if (!node.IsInline) return true;
            var marks = node.Marks;
            if (!mark.IsInSet(marks) && parent!.Type.AllowsMarkType(mark.Type)) {
                int start = Math.Max(pos, from), end = Math.Min(pos + node.NodeSize, to);
                var newSet = mark.AddToSet(marks);

                for (var i = 0; i < marks.Count; i++) {
                    if (!marks[i].IsInSet(newSet)) {
                    if (removing is not null && removing.To == start && removing.Mark.Eq(marks[i]))
                        removing.To = end;
                    else
                        removed.Add(removing = new RemoveMarkStep(start, end, marks[i]));
                    }
                }

                if (adding is not null && adding.To == start)
                    adding.To = end;
                else
                    added.Add(adding = new AddMarkStep(start, end, mark));
            }
            return true;
        });

        removed.ForEach(s => tr.Step(s));
        added.ForEach(s => tr.Step(s));
    }


    private record Match(Model.Mark style, int from, int to, int step) {
        public int to { get; set; } = to;
        public int step { get; set; } = step;
    }
    public static void RemoveMark(Transform tr, int from, int to, Model.Mark mark) =>
        RemoveMark(tr, from, to, (object)mark);
    public static void RemoveMark(Transform tr, int from, int to, MarkType mark) =>
        RemoveMark(tr, from, to, (object)mark);
    internal static void RemoveMark(Transform tr, int from, int to, object? mark) {
        var matched = new List<Match>();
        var step = 0;
        tr.Doc.NodesBetween(from, to, (node, pos, _, _) => {
            if (!node.IsInline) return true;
            step++;
            List<Model.Mark>? toRemove = null;
            if (mark is MarkType markType) {
                var set = node.Marks;
                Model.Mark? found = null;
                while ((found = markType.IsInSet(set)) is not null) {
                    (toRemove ??= new()).Add(found);
                    set = found.RemoveFromSet(set);
                }
            } else if (mark is Model.Mark _mark) {
                if (_mark.IsInSet(node.Marks)) toRemove = new() { _mark };
            } else {
                toRemove = node.Marks;
            }
            if (toRemove?.Count > 0) {
                var end = Math.Min(pos + node.NodeSize, to);
                for (var i = 0; i < toRemove.Count; i++) {
                    var style = toRemove[i];
                    Match? found = null;
                    for (var j = 0; j < matched.Count; j++) {
                        var m = matched[j];
                        if (m.step == step - 1 && style.Eq(matched[j].style)) found = m;
                    }
                    if (found is not null) {
                        found.to = end;
                        found.step = step;
                    } else {
                        matched.Add(new(style, from: Math.Max(pos, from), to: end, step));
                    }
                }
            }
            return true;
        });
        matched.ForEach(m => tr.Step(new RemoveMarkStep(m.from, m.to, m.style)));
    }

    public static void ClearIncompatible(Transform tr, int pos, NodeType parentType, ContentMatch? match = null) {
        match ??= parentType.ContentMatch;
        var node = tr.Doc.NodeAt(pos)!;
        var replSteps = new List<Step>();
        var cur = pos + 1;
        for (var i = 0; i < node.ChildCount; i++) {
            var child = node.Child(i);
            var end = cur + child.NodeSize;
            var allowed = match.MatchType(child.Type);
            if (allowed is null) {
                replSteps.Add(new ReplaceStep(cur, end, Slice.Empty));
            } else {
                match = allowed;
                for (var j = 0; j < child.Marks.Count; j++) if (!parentType.AllowsMarkType(child.Marks[j].Type))
                    tr.Step(new RemoveMarkStep(cur, end, child.Marks[j]));

                if (child.IsText && !(parentType.Spec.Code ?? false)) {
                    var newLine = _NewLineRegex();
                    Slice? slice = null;
                    foreach (var m in newLine.Matches(child.Text!).ToList()) {
                        slice ??= new Slice(Fragment.From(parentType.Schema.Text(" ", parentType.AllowedMarks(child.Marks))),
                                            0, 0);
                        replSteps.Add(new ReplaceStep(cur + m.Index, cur + m.Index + m.Groups[0].Length, slice));
                    }
                }
            }
            cur = end;
        }
        if (!match.ValidEnd) {
            var fill = match.FillBefore(Fragment.Empty, true);
            tr.Replace(cur, cur, new Slice(fill!, 0, 0));
        }
        for (var i = replSteps.Count - 1; i >= 0; i--) tr.Step(replSteps[i]);
    }
}