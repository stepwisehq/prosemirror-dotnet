using System.Text.Json.Serialization;
using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

file static class Util {
    public static Fragment MapFragment(Fragment fragment, Func<Node, Node, int, Node> f, Node parent) {
        var mapped = new List<Node>();
        for (var i = 0; i < fragment.ChildCount; i++) {
            var child = fragment.Child(i);
            if (child.Content.Size > 0) child = child.Copy(Util.MapFragment(child.Content, f, child));
            if (child.IsInline) child = f(child, parent, i);
            mapped.Add(child);
        }
        return Fragment.FromArray(mapped);
    }
}

public class AddMarkStep : Step {
    public int From { get; }
    public int To { get; set; }
    public Model.Mark Mark { get; }

    public AddMarkStep(int from, int to, Model.Mark mark) {
        From = from;
        To = to;
        Mark = mark;
    }

    public override StepResult Apply(Node doc) {
        var oldSlice = doc.Slice(From, To);
        var from = doc.Resolve(From);
        var parent = from.Node(from.SharedDepth(To));
        var slice = new Slice(Util.MapFragment(oldSlice.Content, (node, parent, _) => {
            if (!node.IsAtom || !parent.Type.AllowsMarkType(Mark.Type)) return node;
            return node.Mark(Mark.AddToSet(node.Marks));
        }, parent), oldSlice.OpenStart, oldSlice.OpenEnd);
        return StepResult.FromReplace(doc, From, To, slice);
    }

    public override RemoveMarkStep Invert(Node doc) =>
        new(From, To, Mark);

    public override AddMarkStep? Map(IMappable mapping) {
        var from = mapping.MapResult(From, 1);
        var to = mapping.MapResult(To, -1);
        if (from.Deleted && to.Deleted || from.Pos >= to.Pos) return null;
        return new AddMarkStep(from.Pos, to.Pos, Mark);
    }

    public override AddMarkStep? Merge(Step other) {
        if (other is AddMarkStep am &&
            am.Mark.Eq(Mark) &&
            From <= am.To && To >= am.From)
            return new AddMarkStep(Math.Min(From, am.From),
                                   Math.Max(To, am.To), Mark);
        return null;
    }

    public override AddMarkStepDto ToJSON() =>
        new() {
            Mark = Mark.ToJSON(),
            From = From,
            To = To,
        };

    public static AddMarkStep FromJSON(Schema schema, AddMarkStepDto json) =>
        new(json.From, json.To, schema.MarkFromJSON(json.Mark));
}

public class RemoveMarkStep : Step {
    public int From { get; }
    public int To { get; set; }
    public Model.Mark Mark { get; }

    public RemoveMarkStep(int from, int to, Model.Mark mark) {
        From = from;
        To = to;
        Mark = mark;
    }

    public override StepResult Apply(Node doc) {
        var oldSlice = doc.Slice(From, To);
        var slice = new Slice(Util.MapFragment(oldSlice.Content, (node, _, _) => {
            return node.Mark(Mark.RemoveFromSet(node.Marks));
        }, doc), oldSlice.OpenStart, oldSlice.OpenEnd);
        return StepResult.FromReplace(doc, From, To, slice);
    }

    public override AddMarkStep Invert(Node doc) =>
        new(From, To, Mark);

    public override RemoveMarkStep? Map(IMappable mapping) {
        var from = mapping.MapResult(From, 1);
        var to = mapping.MapResult(To, -1);
        if (from.Deleted && to.Deleted || from.Pos >= to.Pos) return null;
        return new(from.Pos, to.Pos, Mark);
    }

    public override RemoveMarkStep? Merge(Step other) {
        if (other is RemoveMarkStep rm &&
            rm.Mark.Eq(Mark) &&
            From <= rm.To && To >= rm.From)
            return new RemoveMarkStep(Math.Min(From, rm.From),
                                   Math.Max(To, rm.To), Mark);
        return null;
    }

    public override RemoveMarkStepDto ToJSON() =>
        new() {
            Mark = Mark.ToJSON(),
            From = From,
            To = To,
        };

    public static RemoveMarkStep FromJSON(Schema schema, RemoveMarkStepDto json) =>
        new(json.From, json.To, schema.MarkFromJSON(json.Mark));
}

public class AddNodeMarkStep : Step {
    public int Pos { get; }
    public Model.Mark Mark { get; }

    public AddNodeMarkStep(int pos, Model.Mark mark) {
        Pos = pos;
        Mark = mark;
    }

    public override StepResult Apply(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is null) return StepResult.Fail("No node at mark step's position");
        var updated = node.Type.Create(node.Attrs, (Node?)null, Mark.AddToSet(node.Marks));
        return StepResult.FromReplace(doc, Pos, Pos + 1, new Slice(Fragment.From(updated), 0, node.IsLeaf ? 0 : 1));
    }

    public override Step Invert(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is not null) {
            var newSet = Mark.AddToSet(node.Marks);
            if (newSet.Count == node.Marks.Count) {
                for (var i = 0; i < node.Marks.Count; i++)
                    if (!node.Marks[i].IsInSet(newSet))
                        return new AddNodeMarkStep(Pos, node.Marks[i]);
                return new AddNodeMarkStep(Pos, Mark);
            }
        }
        return new RemoveNodeMarkStep(Pos, Mark);
    }

    public override AddNodeMarkStep? Map(IMappable mapping) {
        var pos = mapping.MapResult(Pos, 1);
        return pos.DeletedAfter ? null : new AddNodeMarkStep(Pos, Mark);
    }

    public override AddNodeMarkStepDto ToJSON() =>
        new() {
            Pos = Pos,
            Mark = Mark.ToJSON(),
        };

    public static AddNodeMarkStep FromJSON(Schema schema, AddNodeMarkStepDto json) =>
        new(json.Pos, schema.MarkFromJSON(json.Mark));
}

public class RemoveNodeMarkStep : Step {
    public int Pos { get; }
    public Model.Mark Mark { get; }

    public RemoveNodeMarkStep(int pos, Model.Mark mark) {
        Pos = pos;
        Mark = mark;
    }

    public override StepResult Apply(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is null) return StepResult.Fail("No node at mark step's position");
        var updated = node.Type.Create(node.Attrs, (Node?)null, Mark.RemoveFromSet(node.Marks));
        return StepResult.FromReplace(doc, Pos, Pos + 1, new Slice(Fragment.From(updated), 0, node.IsLeaf ? 0 : 1));
    }

    public override Step Invert(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is null || !Mark.IsInSet(node.Marks)) return this;
        return new AddNodeMarkStep(Pos, Mark);
    }

    public override RemoveNodeMarkStep? Map(IMappable mapping) {
        var pos = mapping.MapResult(Pos, 1);
        return pos.DeletedAfter ? null : new RemoveNodeMarkStep(Pos, Mark);
    }

    public override RemoveNodeMarkStepDto ToJSON() =>
        new() {
            Pos = Pos,
            Mark = Mark.ToJSON(),
        };

    public static RemoveNodeMarkStep FromJSON(Schema schema, RemoveNodeMarkStepDto json) =>
        new(json.Pos, schema.MarkFromJSON(json.Mark));
}


public record AddMarkStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "addMark";
    public required MarkDto Mark { get; init; }
    public required int From { get; init; }
    public required int To { get; init; }
}

public record RemoveMarkStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "removeMark";
    public required MarkDto Mark { get; init; }
    public required int From { get; init; }
    public required int To { get; init; }
}

public record AddNodeMarkStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "addNodeMark";
    public required int Pos { get; init; }
    public required MarkDto Mark { get; init; }
}

public record RemoveNodeMarkStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "removeNodeMark";
    public required int Pos { get; init; }
    public required MarkDto Mark { get; init; }
}