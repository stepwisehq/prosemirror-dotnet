using System.Text.Json.Nodes;

using OneOf;

using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

using static Replace;

public class TransformException : Exception {
    public TransformException() : base() { }
    public TransformException(string message) : base(message) {}
    public TransformException(string message, Exception innerException): base(message, innerException) {}
}


public class Transform {
    public List<Step> Steps { get; } = new();
    public List<Node> Docs { get; } = new();
    public Node Doc { get; private set; }
    public Mapping Mapping { get; } = new();

    public Transform(Node doc) {
        Doc = doc;
    }

    public Node Before => Docs.Count > 0 ? Docs[0] : Doc;

    public Transform Step(Step step) {
        var result = MaybeStep(step);
        if (result.Failed is not null) throw new TransformException(result.Failed);
        return this;
    }

    public StepResult MaybeStep(Step step) {
        var result = step.Apply(Doc);
        if (result.Failed is null) AddStep(step, result.Doc!);
        return result;
    }

    public bool DocChanged => Steps.Count > 0;

    public void AddStep(Step step, Node doc) {
        Docs.Add(Doc);
        Steps.Add(step);
        Mapping.AppendMap(step.GetMap());
        Doc = doc;
    }

    public Transform Replace(int from, int? to = null, Slice? slice = null) {
        to ??= from;
        slice ??= Slice.Empty;
        var step = replaceStep(Doc, from, to , slice);
        if (step is not null) Step(step);
        return this;
    }

    public Transform ReplaceWith(int from, int to, ContentLike content) {
        return Replace(from, to, new Slice(Fragment.From(content), 0, 0));
    }

    public Transform Delete(int from, int to) {
        return Replace(from, to, Slice.Empty);
    }

    public Transform Insert(int pos, ContentLike content) {
        return ReplaceWith(pos, pos, content);
    }

    public Transform ReplaceRange(int from, int to, Slice slice) {
        Transformation.Replace.ReplaceRange(this, from, to, slice);
        return this;
    }

    public Transform ReplaceRangeWith(int from, int to, Node node) {
        Transformation.Replace.ReplaceRangeWith(this, from, to, node);
        return this;
    }

    public Transform DeleteRange(int from, int to) {
        Transformation.Replace.DeleteRange(this, from, to);
        return this;
    }

    public Transform Lift(NodeRange range, int target) {
        Structure.Lift(this, range, target);
        return this;
    }

    public Transform Join(int pos, int depth = 1) {
        Structure.Join(this, pos, depth);
        return this;
    }

    public Transform Wrap(NodeRange range, List<Wrapper> wrappers) {
        Structure.Wrap(this, range, wrappers);
        return this;
    }

    public Transform SetBlockType(int from, int to, NodeType type, Attrs? attrs = null) {
        Structure.SetBlockType(this, from, to, type, attrs);
        return this;
    }

    public Transform SetNodeMarkup(int pos, NodeType? type = null, Attrs? attrs = null, List<Model.Mark>? marks = null) {
        Structure.SetNodeMarkup(this, pos, type, attrs, marks);
        return this;
    }

    public Transform SetNodeAttribute(int pos, string attr, JsonNode? value) {
        Step(new AttrStep(pos, attr, value));
        return this;
    }

    public Transform SetDocAttribute(string attr, JsonNode? value) {
        Step(new DocAttrStep(attr, value));
        return this;
    }

    public Transform AddNodeMark(int pos, Model.Mark mark) {
        Step(new AddNodeMarkStep(pos, mark));
        return this;
    }

    public Transform RemoveNodeMark(int pos, OneOf<Model.Mark, MarkType> mark) {
        Model.Mark? _mark;
        if (mark.Value is MarkType markType) {
            var node = Doc.NodeAt(pos);
            if (node is null) throw new Exception($"No node at position {pos}");
            _mark = markType.IsInSet(node.Marks)!;
            if (_mark is null) return this;
        } else {
            _mark = (Model.Mark)mark.Value;
        }
        Step(new RemoveNodeMarkStep(pos, _mark));
        return this;
    }

    public Transform Split(int pos, int? depth = null, List<Wrapper?>? typesAfter = null) {
        depth ??= 1;
        Structure.Split(this, pos, depth.Value, typesAfter);
        return this;
    }

    public Transform AddMark(int from, int to, Model.Mark mark) {
        Mark.AddMark(this, from, to, mark);
        return this;
    }

    public Transform RemoveMark(int from, int to, MarkType? mark = null) =>
        RemoveMark(from, to, (object?)mark);
    public Transform RemoveMark(int from, int to, Model.Mark mark) =>
        RemoveMark(from, to, (object)mark);
    private Transform RemoveMark(int from, int to, object? mark) {
        Mark.RemoveMark(this, from, to, mark);
        return this;
    }

    public Transform ClearIncompatible(int pos, NodeType parentType, ContentMatch? match = null) {
        Mark.ClearIncompatible(this, pos, parentType, match);
        return this;
    }
}