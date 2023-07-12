using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using DotNext;
using DotNext.Text.Json;
using Json.More;
using OneOf;

using StepWise.Prose.Collections;

namespace StepWise.Prose.Model;


public class Attrs : Dictionary<string, JsonNode?> {
    public Attrs() : base() {}
    public Attrs(Attrs source) : base(source) {}
    public static bool CompareDeep(Attrs a, Attrs b) {
        if (ReferenceEquals(a, b)) return true;

        // Check for matching keys first.
        foreach (var (key, _) in a) if (!b.ContainsKey(key)) return false;
        foreach (var (key, _) in b) if (!a.ContainsKey(key)) return false;

        // Do basic reference equality check.
        if (a.Select(kv => kv.Key).All(k => ReferenceEquals(a[k], b[k]))) return true;
        // Do deep equality check.
        if (a.Select(kv => kv.Key).All(k => a[k].IsEquivalentTo(b[k]))) {
            return true;
        }

        return false;
    }
}

public class AttributeList : Dictionary<string, Attribute> {
    public static AttributeList InitAttrs(Dictionary<string, AttributeSpec>? specList) {
        var attrList = new AttributeList();
        if (specList is not null)
            foreach (var (name, spec) in specList)
                attrList[name] = new Attribute(spec);
        return attrList;
    }

    public Attrs DefaultAttrs() {
        var defaultAttrs = new Attrs();
        foreach (var (name, attr) in this) {
            if (!attr.HasDefault) return new Attrs();
            defaultAttrs[name] = attr.Default;
        }
        return defaultAttrs;
    }

    public Attrs ComputeAttrs(Attrs? value) {
        var built = new Attrs();
        foreach (var (name, attr) in this) {
            if (!value?.ContainsKey(name) ?? true) {
                if (attr.HasDefault) built[name] = attr.Default;
                else throw new Exception($"No value supplied for attribute {name}");
            } else {
                built[name] = value![name];
            }
        }
        return built;
    }
}

public enum WhiteSpace {
    Pre,
    Normal
}

public class NodeType {
    public List<string> Groups { get; init; }
    public AttributeList Attrs { get; init; }
    public Attrs DefaultAttrs { get; init; }

    public string Name { get; init; }
    public Schema Schema { get; init; }
    public NodeSpec Spec { get; init; }

    public NodeType(
        string name,
        Schema schema,
        NodeSpec spec
    ) {
        Name = name;
        Schema = schema;
        Spec = spec;

        Groups = spec.Group?.Split(' ').ToList() ?? new();
        Attrs = AttributeList.InitAttrs(spec.Attrs ?? new());
        DefaultAttrs = Attrs.DefaultAttrs();

        IsBlock = !(spec.Inline ?? false || name == "text");
        IsText = name == "text";
    }

    private bool? _inlineContent { get; set; } = null;
    public bool InlineContent {
        get {
            if (_inlineContent is null) throw new Exception("NodeType.InlineContent accessed outside Schema");
            return _inlineContent.Value;
        } set { _inlineContent = value; }
    }
    public bool IsBlock { get; init; }
    public bool IsText { get; init; }

    public bool IsInline => !IsBlock;
    public bool IsTextBlock => IsBlock && InlineContent;
    public bool IsLeaf => ContentMatch == ContentMatch.Empty;
    public bool IsAtom => IsLeaf || (Spec.Atom ?? false);

    private ContentMatch? _contentMatch { get; set; } = null;
    public ContentMatch ContentMatch {
        get {
            if (_contentMatch is null) throw new Exception("NodeType.ContentMatch accessed outside Schema");
            return _contentMatch;
        } set { _contentMatch = value; }
    }

    public List<MarkType>? MarkSet { get; set; }

    public WhiteSpace WhiteSpace => Spec.Whitespace ?? (Spec.Code ?? false ? WhiteSpace.Pre : WhiteSpace.Normal);

    public bool HasRequiredAttrs() {
        foreach (var (_, attr) in Attrs) if (attr.IsRequired) return true;
        return false;
    }

    public bool CompatibleContent(NodeType other) {
        return ReferenceEquals(this, other) || ContentMatch.Compatible(other.ContentMatch);
    }

    public Attrs ComputeAttrs(Attrs? attrs) {
        if (attrs is null && DefaultAttrs is not null) return DefaultAttrs;
        else return Attrs.ComputeAttrs(attrs);
    }

    public Node Create(Attrs? attrs, List<Node> content, List<Mark>? marks = null) {
        if (IsText) throw new Exception("NodeType.Create can't construct text nodes");
        return new Node(this, ComputeAttrs(attrs), Fragment.From(content), Mark.SetFrom(marks!));
    }

    public Node Create(Attrs? attrs = null, IContentLike? content = null, List<Mark>? marks = null) {
        if (IsText) throw new Exception("NodeType.Create can't construct text nodes");
        return new Node(this, ComputeAttrs(attrs), Fragment.From(content), Mark.SetFrom(marks!));
    }

    public Node CreateChecked(Attrs? attrs = null, IContentLike? content = null, List<Mark>? marks = null) {
        var _content = Fragment.From(content);
        CheckContent(_content);
        return new Node(this, ComputeAttrs(attrs), _content, Mark.SetFrom(marks));
    }

    public Node? CreateAndFill(Attrs? attrs = null, IContentLike? content = null, List<Mark>? marks = null) {
        var _attrs = ComputeAttrs(attrs);
        var _content = Fragment.From(content);
        if (_content.Size > 0) {
            var before = ContentMatch.FillBefore(_content);
            if (before is null) return null;
            _content = before.Append(_content);
        }
        var matched = ContentMatch.MatchFragment(_content);
        var after = matched?.FillBefore(Fragment.Empty, true);
        if (after is null) return null;
        return new Node(this, _attrs, _content.Append(after), Mark.SetFrom(marks!));
    }

    public bool ValidContent(Fragment content) {
        var result = ContentMatch.MatchFragment(content);
        if (result is null || !result.ValidEnd) return false;
        for (var i = 0; i < content.ChildCount; i++)
            if (!AllowsMarks(content.Child(i).Marks)) return false;
        return true;
    }

    public void CheckContent(Fragment content) {
        if (!ValidContent(content))
            throw new Exception($"Invalid content for node {Name}: {content.ToString().Slice(0, 50)}");
    }

    public bool AllowsMarkType(MarkType markType) {
        return MarkSet is null || MarkSet.Contains(markType);
    }

    public bool AllowsMarks(List<Mark> marks) {
        if (MarkSet is null) return true;
        for (var i = 0; i < marks.Count; i++) if (!AllowsMarkType(marks[i].Type)) return false;
        return true;
    }

    public List<Mark> AllowedMarks(List<Mark> marks) {
        if (MarkSet is null) return marks;
        List<Mark>? copy = null;
        for (var i = 0; i < marks.Count; i++) {
            if (!AllowsMarkType(marks[i].Type)) {
                copy ??= marks.Slice(0, i);
            } else {
                copy?.Add(marks[i]);
            }
        }
        return copy is null ? marks : copy.Count > 0 ? copy : Mark.None;
    }

    public static Dictionary<string, NodeType> Compile(
        OrderedDictionary<string, NodeSpec> nodes,
        Schema schema)
    {
        var result = new Dictionary<string, NodeType>();
        foreach  (var (name, spec) in nodes) {
            result[name] = new NodeType(name, schema, spec);
        }

        var topType = schema.Spec.TopNode ?? "doc";
        if (!result.ContainsKey(topType)) throw new Exception($"Schema is missing its top node type ('{topType}')");
        if (!result.ContainsKey("text")) throw new Exception("Every schema needs a 'text' type");
        foreach (var _ in result["text"].Attrs) throw new Exception("The text node type should not have attributes");

        return result;
    }
}

public class Attribute {
    public bool HasDefault { get; init; }
    public JsonNode? Default { get; init; }
    public bool IsRequired => !HasDefault;

    public Attribute(AttributeSpec spec) {
        HasDefault =  !spec.Default.IsUndefined;
        if (HasDefault && spec.Default.IsNull) Default = null;
        else if (HasDefault) Default = spec.Default.Value;
    }
}

public class MarkType {
    public AttributeList Attrs { get; init; }
    public List<MarkType>? Excluded { get; set; }
    public Mark? Instance { get; init; }

    public string Name { get; init; }
    public int Rank { get; init; }
    public Schema Schema { get; init; }
    public MarkSpec Spec { get; init; }

    public MarkType(string name, int rank, Schema schema, MarkSpec spec) {
        Name = name;
        Rank = rank;
        Schema = schema;
        Spec = spec;
        Attrs = AttributeList.InitAttrs(spec.Attrs);
        var defaults = Attrs.DefaultAttrs();
        Instance = defaults is not null ? new Mark(this, defaults) : null;
    }

    public Mark Create(Attrs? attrs = null) {
        if (attrs is null && Instance is not null) return Instance;
        return new Mark(this, Attrs.ComputeAttrs(attrs));
    }

    public static Dictionary<string, MarkType> Compile(OrderedDictionary<string, MarkSpec> marks, Schema schema) {
        var result = new Dictionary<string, MarkType>();
        var rank = 0;
        foreach (var (name, spec) in marks)
            result[name] = new MarkType(name, rank++, schema, spec);
        return result;
    }

    public List<Mark> RemoveFromSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++) if (ReferenceEquals(set[i].Type, this)) {
            set = set.Slice(0, i);
            set.AddRange(set.Slice(i + 1));
            i--;
        }
        return set;
    }

    public Mark? IsInSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++)
            if (ReferenceEquals(set[i].Type, this)) return set[i];
        return null;
    }

    public bool Excludes(MarkType other) {
        if (Excluded is null) throw new Exception("Excludes called outside Schema");
        return Excluded.Contains(other);
    }
}

public interface ISchemaSpec {
    public OrderedDictionary<string, NodeSpec> Nodes { get; init; }
    public OrderedDictionary<string, MarkSpec>? Marks { get; }
    public string? TopNode { get; init; }
}

public class SchemaSpec: ISchemaSpec {
    public required OrderedDictionary<string, NodeSpec> Nodes { get; init; }
    public OrderedDictionary<string, MarkSpec>? Marks { get; init; }
    public string? TopNode { get; init; }
}

public class NodeSpec {
    public string? Content { get; init; }
    public string? Marks { get; init; }
    public string? Group { get; init; }
    public bool? Inline { get; init; }
    public bool? Atom { get; init; }
    public Dictionary<string, AttributeSpec>? Attrs { get; init; }
    public bool? Selectable { get; init; }
    public bool? Draggable { get; init; }
    public bool? Code { get; init; }
    public WhiteSpace? Whitespace { get; init; }
    public bool? DefiningAsContext { get; init; }
    public bool? DefiningForContext { get; init; }
    public bool? Defining { get; init; }
    public bool? Isolating { get; init; }

    public Func<Node, string>? ToDebugString { get; init; }
    public Func<Node, string>? LeafText { get; init; }
}

public class MarkSpec {
    public Dictionary<string, AttributeSpec>? Attrs { get; init; }
    public bool? Inclusive { get; init; }
    public string? Excludes { get; init; }
    public string? Group { get; init; }
    public bool? Spanning { get; init; }
}

public class AttributeSpec {
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<JsonNode> Default { get; init; }
}

public class Schema {
    public ActiveSchemaSpec Spec { get; init; }
    public Dictionary<string, NodeType> Nodes{ get; init; }
    public Dictionary<string, MarkType> Marks { get; init; }

    public class ActiveSchemaSpec : ISchemaSpec {
        public required OrderedDictionary<string, NodeSpec> Nodes { get; init; }
        public required OrderedDictionary<string, MarkSpec> Marks { get; init; }
        public string? TopNode { get; init; }
    }

    public Schema(SchemaSpec spec) {
        Spec = new ActiveSchemaSpec {
            Nodes = spec.Nodes,
            Marks = spec.Marks ?? new(),
            TopNode = spec.TopNode
        };

        Nodes = Model.NodeType.Compile(Spec.Nodes, this);
        Marks = MarkType.Compile(Spec.Marks, this);

        var contentExprCache = new Dictionary<string, ContentMatch>();
        foreach (var (prop, _) in Nodes) {
            if (Marks.ContainsKey(prop))
                throw new Exception($"{prop} can not be both a node and a mark");
            var type = Nodes[prop];
            var contentExpr = type.Spec.Content ?? "";
            var markExpr = type.Spec.Marks;
            if (contentExprCache.TryGetValue(contentExpr, out var cached)) {
                type.ContentMatch = cached;
            } else {
                type.ContentMatch = ContentMatch.Parse(contentExpr, Nodes);
                contentExprCache[contentExpr] = type.ContentMatch;
            }
            type.InlineContent = type.ContentMatch.InlineContent;
            type.MarkSet = markExpr == "_" ? null :
                markExpr?.Length > 0 ? GatherMarks(markExpr.Split(" ").ToList()) :
                markExpr == string.Empty || !type.InlineContent ? new() : null;
        }
        foreach (var (prop, _) in Marks) {
            var type = Marks[prop];
            var excl = type.Spec.Excludes;
            type.Excluded = excl is null ? new() {type} : excl == string.Empty ? new() : GatherMarks(excl.Split(" ").ToList());
        }

        TopNodeType = Nodes[Spec.TopNode ?? "doc"];
    }

    public NodeType TopNodeType { get; init; }


    public Node Node(
        OneOf<string, NodeType> type,
        Attrs? attrs = null,
        IContentLike? content = null,
        List<Mark>? marks = null)
    {
        var _type = type.Match(
            str => NodeType(str),
            nodeType => nodeType
        );
        if (!ReferenceEquals(_type.Schema, this)) throw new Exception($"Node type from different schema used ({_type.Name})");

        return _type.CreateChecked(attrs, content, marks);
    }

    public TextNode Text(string text, List<Mark>? marks = null) {
        var type = Nodes["text"];
        return new TextNode(type, type.DefaultAttrs, text, Model.Mark.SetFrom(marks!));
    }

    public Mark Mark(OneOf<string, MarkType> type, Attrs? attrs = null) {
        var _type = type.Match(
            str => Marks[str],
            markType => markType
        );
        return _type.Create(attrs);
    }

    public Node NodeFromJSON(NodeDto json) {
        return Model.Node.FromJSON(this, json);
    }

    public Mark MarkFromJSON(MarkDto json) {
        return Model.Mark.FromJSON(this, json);
    }

    public NodeType NodeType(string name) {
        if (Nodes.TryGetValue(name, out var node)) return node;
        throw new Exception($"Unknown node type: '{name}'");
    }

    public List<MarkType> GatherMarks(List<string> marks) {
        List<MarkType> found = new();
        for (var i = 0; i < marks.Count; i++) {
            var name = marks[i];
            var ok = false;
            if (Marks.TryGetValue(name, out var mark)) {
                ok = true;
                found.Add(mark);
            } else {
                foreach (var (prop, _) in Marks) {
                    mark = Marks[prop];
                    if (name == "_" || (mark.Spec.Group?.Split(" ").Contains(name) ?? false)) {
                        found.Add(mark);
                        ok = true;
                    }
                }
            }
            if (!ok) throw new Exception($"Unknown mark type: '{name}'");
        }
        return found;
    }
}

public static class NullableExtensions {
    public static bool AsBool(this bool? b) => b ?? false;
}