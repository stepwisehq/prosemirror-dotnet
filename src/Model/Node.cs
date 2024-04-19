using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using OneOf;

using StepWise.Prose.Collections;
using StepWise.Prose.Text.Json;


namespace StepWise.Prose.Model;

using OptionalAttrs = DotNext.Optional<Dictionary<string, DotNext.Optional<JsonNode>>>;
public class NodeList : List<Node>, IContentLike {}

public class Node : IContentLike {
    public NodeType Type { get; init; }
    public Attrs Attrs { get; init; }
    public Fragment Content { get; init; } = new(new(), 0);
    public List<Mark> Marks { get; init; } = Model.Mark.None;

    public virtual string? Text { get; }

    public Node(
        NodeType type,
        Attrs attrs,
        Fragment? content,
        List<Mark>? marks = null)
    {
        Type = type;
        Attrs = attrs;
        Content = content ?? Fragment.Empty;
        Marks = marks ?? Model.Mark.None;
    }


    public virtual int NodeSize => IsLeaf ? 1 : 2 + Content.Size;

    public int ChildCount => Content.ChildCount;

    public Node Child(int index) => Content.Child(index);

    public Node? MaybeChild(int index) => Content.MaybeChild(index);

    public void ForEach(Action<Node, int, int> f) => Content.ForEach(f);

    public void NodesBetween(
        int from,
        int to,
        Func<Node, int, Node?, int, bool> f,
        int startPos = 0)
    {
        Content.NodesBetween(from, to, f, startPos, this);
    }

    public void Descendants(Func<Node, int, Node?, int, bool> f) => NodesBetween(0, Content.Size, f);

    public virtual string TextContent => IsLeaf && (Type.Spec.LeafText is not null)
        ? Type.Spec.LeafText(this)
        : TextBetween(0, Content.Size, "");

    public string TextBetween(int from, int to, string? blockSeparator, string leafText) =>
        TextBetween(from, to, blockSeparator, _ => leafText);
    public string TextBetween(int from, int to, string? blockSeparator = null, Func<Node, string>? leafText = null) =>
        Content.TextBetween(from, to, blockSeparator, leafText);

    public Node? FirstChild => Content.FirstChild;

    public Node? LastChild => Content.LastChild;

    public virtual bool Eq(Node other) {
        return ReferenceEquals(this, other) || (SameMarkup(other) && Content.Eq(other.Content));
    }

    public bool SameMarkup(Node other) {
        return HasMarkup(other.Type, other.Attrs, other.Marks);
    }

    public bool HasMarkup(NodeType type, Attrs? attrs, List<Mark>? marks = null) {
        return Type == type &&
            Attrs.CompareDeep(Attrs, attrs ?? Type.DefaultAttrs ?? new()) &&
            Model.Mark.SameSet(Marks, marks ?? Model.Mark.None);
    }

    public Node Copy(Fragment? content = null) {
        if (ReferenceEquals(content, Content)) return this;
        return new Node(Type, Attrs, content, Marks);
    }

    public virtual Node Mark(List<Mark> marks) {
        return ReferenceEquals(marks, Marks) ? this : new Node(Type, Attrs, Content, marks);
    }

    public virtual Node Cut(int from , int? to = null) {
        var _to = to ?? Content.Size;
        if (from == 0 && _to == Content.Size) return this;
        return Copy(Content.Cut(from, _to));
    }

    public Slice Slice(int from) => Slice(from, Content.Size);
    public Slice Slice(int from, int? to, bool includeParents = false) {
        to ??= Content.Size;
        if (from == to) return Model.Slice.Empty;

        var _from = Resolve(from);
        var _to = Resolve(to.Value);
        var depth = includeParents ? 0 : _from.SharedDepth(to.Value);
        var start = _from.Start(depth);
        var node = _from.Node(depth);
        var content = node.Content.Cut(_from.Pos - start, _to.Pos - start);
        return new Slice(content, _from.Depth - depth, _to.Depth - depth);
    }

    public Node Replace(int from, int to, Slice slice) => ReplaceUtils.Replace(Resolve(from), Resolve(to), slice);

    public Node? NodeAt(int pos) {
        for (var node = this ;;) {
            var (index, offset) = node.Content.FindIndex(pos);
            node = node.MaybeChild(index);
            if (node is null) return null;
            if (offset == pos || node.IsText) return node;
            pos -= offset + 1;
        }
    }

    public PositionedNode ChildAfter(int pos) {
        var (index, offset) = Content.FindIndex(pos);
        return new(Content.MaybeChild(index), index, offset);
    }

    public PositionedNode ChildBefore(int pos) {
        if (pos == 0) return new(null, 0, 0);
        var (index, offset) = Content.FindIndex(pos);
        if (offset < pos) return new(Content.Child(index), index, offset);
        var node = Content.Child(index - 1);
        return new(node, index - 1, offset - node.NodeSize);
    }

    public ResolvedPos Resolve(int pos) => ResolvedPos.ResolveCached(this, pos);

    public ResolvedPos ResolveNoCache(int pos) => ResolvedPos.Resolve(this, pos);

    public bool RangeHasMark(int from, int to, OneOf<Mark, MarkType> type) {
        var found = false;
        if (to > from) NodesBetween(from, to, (node, _, _, _) => {
            var isInSet = false;
            type.Switch(
                mark => isInSet = mark.IsInSet(node.Marks),
                markType => isInSet = markType.IsInSet(node.Marks) is not null
            );
            if (isInSet) found = true;
            return !found;
        });
        return found;
    }

    public bool IsBlock => Type.IsBlock;

    public bool IsTextBlock => Type.IsTextBlock;

    public bool InlineContent => Type.InlineContent;

    public bool IsInline => Type.IsInline;

    public bool IsText => Type.IsText;

    public bool IsLeaf => Type.IsLeaf;

    public bool IsAtom => Type.IsAtom;

    public override string ToString() {
        if (Type.Spec.ToDebugString is not null) return Type.Spec.ToDebugString(this);
        var name = Type.Name;
        if (Content.Size > 0)
            name += $"({Content.ToStringInner()})";
        return Model.Mark.WrapMarks(Marks, name);
    }

    public ContentMatch ContentMatchAt(int index) {
        var match = Type.ContentMatch.MatchFragment(Content, 0, index);
        if (match is null) throw new Exception("Called ContentMatchAt on a node with invalid content");
        return match;
    }

    public bool CanReplace(int from, int to, Fragment? replacement = null, int start = 0, int? end = null) {
        replacement ??= Fragment.Empty;
        var _end = end ?? replacement.ChildCount;
        var one = ContentMatchAt(from).MatchFragment(replacement, start, _end);
        var two = one?.MatchFragment(Content, to);
        if (two is null || !two.ValidEnd) return false;
        for (var i = start; i < _end; i++)
            if (!Type.AllowsMarks(replacement.Child(i).Marks)) return false;
        return true;
    }

    public bool CanReplaceWith(int from, int to, NodeType type, List<Mark>? marks = null) {
        if (marks is not null && !Type.AllowsMarks(marks)) return false;
        var start = ContentMatchAt(from).MatchType(type);
        var end = start?.MatchFragment(Content, to);
        return end?.ValidEnd ?? false;
    }

    public bool CanAppend(Node other) {
        if (other.Content.Size > 0) return CanReplace(ChildCount, ChildCount, other.Content);
        else return Type.CompatibleContent(other.Type);
    }

    public void Check() {
        Type.CheckContent(Content);
        var copy = Model.Mark.None;
        for (var i = 0; i < Marks.Count; i++) copy = Marks[i].AddToSet(copy);
        if (!Model.Mark.SameSet(copy, Marks))
            throw new Exception($"Invalid collection of marks for node {Type.Name}: {Marks.Select(m => m.Type.Name)}");
        Content.ForEach((node,_,_) => node.Check());
    }

    public virtual NodeDto ToJSON() {
        var attrs = Attrs.Count == 0 ? null : new Dictionary<string, DotNext.Optional<JsonNode>>();
        if (attrs is not null) foreach (var (name, value) in Attrs) attrs[name] = value;

        return new() {
            Type = Type.Name,
            Attrs = attrs is not null ? attrs : OptionalAttrs.None,
            Content = Content?.ChildCount > 0 ? Content.ToJSON() : DotNext.Optional<List<NodeDto>>.None,
            Marks = Marks.Count > 0 ? Marks.Select(m => m.ToJSON()).ToList() : DotNext.Optional<List<MarkDto>>.None,
            Text = DotNext.Optional<string>.None,
        };
    }

    public static Node FromJSON(Schema schema, NodeDto json) {
        List<Mark>? marks = null;
        if (json.Marks.HasValue)
            marks = json.Marks.Value.Select(schema.MarkFromJSON).ToList();
        if (json.Type == "text" && json.Text.HasValue)
            return schema.Text(json.Text.Value, marks);

        var content = Fragment.FromJSON(schema, json.Content.HasValue ? json.Content.Value : null);
        var attrs = new Attrs();
        var jsonAttrs = json.Attrs.HasValue ? json.Attrs.Value : null;
        if (jsonAttrs is not null) foreach (var (name, value) in jsonAttrs) attrs[name] = value.IsNull ? null : value.Value;
        return schema.NodeType(json.Type).Create(attrs, content, marks);
    }
}

public class TextNode : Node {
    public override string Text { get; }

    public TextNode(NodeType type, Attrs attrs, string content, List<Mark>? marks = null) : base(
        type,
        attrs,
        null,
        marks)
    {
        if (content == string.Empty) throw new ArgumentException("Empty text nodes are not allowed");
        Text = content;
    }

    public override string ToString() {
        if (Type.Spec.ToDebugString is not null) return Type.Spec.ToDebugString(this);
        return Model.Mark.WrapMarks(Marks, JsonSerializer.Serialize(Text));
    }

    public override string TextContent => Text;

    public string TextBetween(int from , int to) => Text[from..to];

    public override int NodeSize => Text.Length;

    public override TextNode Mark(List<Mark> marks) {
        return ReferenceEquals(marks, Marks) ? this : new TextNode(Type, Attrs, Text, marks);
    }

    public TextNode WithText(string text) {
        if (text == Text) return this;
        return new TextNode(Type, Attrs, text, Marks);
    }

    public override TextNode Cut(int from = 0, int? to = null) {
        var _to = to ?? Text.Length;
        if (from == 0 && to == Text.Length) return this;
        return WithText(Text.slice(from, _to));
    }

    public override bool Eq(Node other) {
        return SameMarkup(other) && Text == other.Text;
    }

    public override NodeDto ToJSON() {
        var dto = base.ToJSON();
        dto.Text = Text;
        return dto;
    }
}

public record PositionedNode(Node? Node, int Index, int Offset) {}

public record NodeDto {
    public required string Type { get; init; }
    // Use of optional dictionary values here is to work around a bug in deserializing null JsonNodes
    // https://github.com/dotnet/runtime/issues/85172
    // Wasn't backported to Dotnet 7 so can remove when DotNet 8 is released.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalAttrs Attrs { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<List<NodeDto>> Content { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<string> Text { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<List<MarkDto>> Marks { get; init; }

    public JsonElement ToJsonElement() =>
        JsonSerializer.SerializeToElement(this, ProseJson.JsonOptions);
    public string ToJson() =>
        JsonSerializer.Serialize(this, ProseJson.JsonOptions);

    public static NodeDto FromJson(string json) =>
        JsonSerializer.Deserialize<NodeDto>(json, ProseJson.JsonOptions)!;
    public static NodeDto FromJson(JsonElement json) =>
        JsonSerializer.Deserialize<NodeDto>(json, ProseJson.JsonOptions)!;
}