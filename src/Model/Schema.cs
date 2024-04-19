using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using DotNext;
using DotNext.Text.Json;
using Json.More;
using OneOf;

using StepWise.Prose.Collections;


namespace StepWise.Prose.Model;

/// <summary>
/// An object holding the attributes of a node.
/// </summary>
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

    /// <summary>
    /// For node types where all attrs have a default value (or which don't
    /// have any attributes), build up a single reusable default attribute
    /// object, and use it for all nodes that don't specify specific
    /// attributes.
    /// </summary>
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

/// <summary>
/// Node types are objects allocated once per <c>Schema</c> and used to
/// <see cref="Model.Node.Type">Tag</see> <c>Node</c> instances. They contain information
/// about the node type, such as its name and what kind of node it
/// represents.
/// </summary>
public class NodeType {
    public List<string> Groups { get; init; }
    public AttributeList Attrs { get; init; }
    public Attrs DefaultAttrs { get; init; }

    /// <summary>The name the node type has in this schema.</summary>
    public string Name { get; init; }
    /// <summary>A link back to the <see cref="Model.Schema"/> the node type belongs to.</summary>
    public Schema Schema { get; init; }
    /// <summary>The spec that this type is based on</summary>
    public NodeSpec Spec { get; init; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="name">The name the node type has in this schema.</param>
    /// <param name="schema">A link back to the <see cref="Model.Schema"/> the node type belongs to.</param>
    /// <param name="spec">The spec that this type is based on</param>
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
    /// <summary>True if this node type has inline content.</summary>
    public bool InlineContent {
        get {
            if (_inlineContent is null) throw new Exception("NodeType.InlineContent accessed outside Schema");
            return _inlineContent.Value;
        } set { _inlineContent = value; }
    }
    /// <summary>True if this is a block type</summary>
    public bool IsBlock { get; init; }
    /// <summary>True if this is the text node type.</summary>
    public bool IsText { get; init; }
    /// <summary>True if this is an inline type.</summary>
    public bool IsInline => !IsBlock;
    /// <summary>True if this is a textblock type, a block that contains inline
    /// content.</summary>
    public bool IsTextBlock => IsBlock && InlineContent;
    /// <summary>True for node types that allow no content.</summary>
    public bool IsLeaf => ContentMatch == ContentMatch.Empty;
    /// <summary>True when this node is an atom, i.e. when it does not have
    /// directly editable content.</summary>
    public bool IsAtom => IsLeaf || (Spec.Atom ?? false);

    private ContentMatch? _contentMatch { get; set; } = null;
    /// <summary>The starting match of the node type's content expression.</summary>
    public ContentMatch ContentMatch {
        get {
            if (_contentMatch is null) throw new Exception("NodeType.ContentMatch accessed outside Schema");
            return _contentMatch;
        } set { _contentMatch = value; }
    }
    /// <summary>The set of marks allowed in this node. <c>null</c> means all marks
    /// are allowed.</summary>
    public List<MarkType>? MarkSet { get; set; }
    /// <summary>The node type's <see cref="Model.NodeSpec.Whitespace"/> option.</summary>
    public WhiteSpace WhiteSpace => Spec.Whitespace ?? (Spec.Code ?? false ? WhiteSpace.Pre : WhiteSpace.Normal);
    /// <summary>Tells you whether this node type has any required attributes.</summary>
    public bool HasRequiredAttrs() {
        foreach (var (_, attr) in Attrs) if (attr.IsRequired) return true;
        return false;
    }
    /// <summary>Indicates whether this node allows some of the same content as
    /// the given node type.</summary>
    public bool CompatibleContent(NodeType other) {
        return ReferenceEquals(this, other) || ContentMatch.Compatible(other.ContentMatch);
    }

    public Attrs ComputeAttrs(Attrs? attrs) {
        if (attrs is null && DefaultAttrs is not null) return DefaultAttrs;
        else return Attrs.ComputeAttrs(attrs);
    }

    /// <summary>Create a <c>Node</c> of this type. The given attributes are
    /// checked and defaulted (you can pass <c>null</c> to use the type's
    /// defaults entirely, if no required attributes exist). <c>content</c>
    /// may be a <c>Fragment</c>, a node, an array of nodes, or
    /// <c>null</c>. Similarly <c>marks</c> may be <c>null</c> to default to the empty
    /// set of marks.</summary>
    public Node Create(Attrs? attrs, List<Node> content, List<Mark>? marks = null) {
        if (IsText) throw new Exception("NodeType.Create can't construct text nodes");
        return new Node(this, ComputeAttrs(attrs), Fragment.From(content), Mark.SetFrom(marks!));
    }

    /// <inheritdoc cref="Create(Attrs?, List{Node}, List{Mark}?)"/>
    public Node Create(Attrs? attrs = null, IContentLike? content = null, List<Mark>? marks = null) {
        if (IsText) throw new Exception("NodeType.Create can't construct text nodes");
        return new Node(this, ComputeAttrs(attrs), Fragment.From(content), Mark.SetFrom(marks!));
    }

    /// <summary>Like <see cref="Create(Attrs?, List{Node}, List{Mark}?)"><c>Create</c></see>, but check the given content
    /// against the node type's content restrictions, and throw an error
    /// if it doesn't match.</summary>
    public Node CreateChecked(Attrs? attrs = null, IContentLike? content = null, List<Mark>? marks = null) {
        var _content = Fragment.From(content);
        CheckContent(_content);
        return new Node(this, ComputeAttrs(attrs), _content, Mark.SetFrom(marks));
    }

    /// <summary>Like <see cref="Create(Attrs?, List{Node}, List{Mark}?)"><c>Create</c></see>, but see if it is
    /// necessary to add nodes to the start or end of the given fragment
    /// to make it fit the node. If no fitting wrapping can be found,
    /// return null. Note that, due to the fact that required nodes can
    /// always be created, this will always succeed if you pass null or
    /// <c>Fragment.Empty</c> as content.</summary>
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

    /// <summary>Returns true if the given fragment is valid content for this node
    /// type with the given attributes.</summary>
    public bool ValidContent(Fragment content) {
        var result = ContentMatch.MatchFragment(content);
        if (result is null || !result.ValidEnd) return false;
        for (var i = 0; i < content.ChildCount; i++)
            if (!AllowsMarks(content.Child(i).Marks)) return false;
        return true;
    }

    /// <exception>Throws a RangeError if the given fragment is not valid content for this
    /// node type.</exception>
    public void CheckContent(Fragment content) {
        if (!ValidContent(content))
            throw new Exception($"Invalid content for node {Name}: {content.ToString().slice(0, 50)}");
    }

    /// <summary>Check whether the given mark type is allowed in this node.</summary>
    public bool AllowsMarkType(MarkType markType) {
        return MarkSet is null || MarkSet.Contains(markType);
    }

    /// <summary>Test whether the given set of marks are allowed in this node.</summary>
    public bool AllowsMarks(List<Mark> marks) {
        if (MarkSet is null) return true;
        for (var i = 0; i < marks.Count; i++) if (!AllowsMarkType(marks[i].Type)) return false;
        return true;
    }

    /// <summary>Removes the marks that are not allowed in this node from the given set.</summary>
    public List<Mark> AllowedMarks(List<Mark> marks) {
        if (MarkSet is null) return marks;
        List<Mark>? copy = null;
        for (var i = 0; i < marks.Count; i++) {
            if (!AllowsMarkType(marks[i].Type)) {
                copy ??= marks.slice(0, i);
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

/// <summary>Like nodes, marks (which are associated with nodes to signify
/// things like emphasis or being part of a link) are
/// <see cref="Mark.Type">tagged</see>  with type objects, which are
/// instantiated once per <c>Schema</c>.</summary>
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

    /// <summary>Create a mark of this type. <c>attrs</c> may be <c>null</c> or an object
    /// containing only some of the mark's attributes. The others, if
    /// they have defaults, will be added.</summary>
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

    /// <summary>When there is a mark of this type in the given set, a new set
    /// without it is returned. Otherwise, the input set is returned.</summary>
    public List<Mark> RemoveFromSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++) if (ReferenceEquals(set[i].Type, this)) {
            set = set.slice(0, i);
            set.AddRange(set.slice(i + 1));
            i--;
        }
        return set;
    }

    /// <summary>Tests whether there is a mark of this type in the given set.</summary>
    public Mark? IsInSet(List<Mark> set) {
        for (var i = 0; i < set.Count; i++)
            if (ReferenceEquals(set[i].Type, this)) return set[i];
        return null;
    }

    /// <summary>Queries whether a given mark type is
    /// <see cref="MarkSpec.Excludes">excluded</see> by this one.</summary>
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

/// <summary>An object describing a schema, as passed to the [`Schema`](#model.Schema)
/// constructor.</summary>
public class SchemaSpec: ISchemaSpec {
    /// <summary>The node types in this schema. Maps names to <see cref="NodeSpec"><c>NodeSpec</c></see>
    /// objects that describe the node type
    /// associated with that name. Their order is significant—it
    /// determines which <see href="https://prosemirror.net/docs/guide/#model.NodeSpec.parseDOM">parse rules</see>
    /// take precedence by default, and which nodes come first in a given
    /// <see cref="NodeSpec.Group">group</see>.</summary>
    public required OrderedDictionary<string, NodeSpec> Nodes { get; init; }

    /// <summary>The mark types that exist in this schema. The order in which they
    /// are provided determines the order in which <see cref="Mark.AddToSet(List{Mark})">mark
    /// sets</see> are sorted and in which <see href="https://prosemirror.net/docs/guide/#model.MarkSpec.parseDOM">parse
    /// rules</see> are tried.</summary>
    public OrderedDictionary<string, MarkSpec>? Marks { get; init; }

    /// <summary>The name of the default top-level node for the schema. Defaults
    /// to <c>"doc"</c>.</summary>
    public string? TopNode { get; init; }
}

/// <summary>A description of a node type, used when defining a schema.</summary>
public class NodeSpec {
    /// <summary>The content expression for this node, as described in the
    /// <see href="https://prosemirror.net/docs/guide/#schema.content_expressions">schema guide</see>.
    /// When not given, the node does not allow any content.</summary>
    public string? Content { get; init; }

    /// <summary>The marks that are allowed inside of this node. May be a
    /// space-separated string referring to mark names or groups, <c>"_"</c>
    /// to explicitly allow all marks, or <c>""</c> to disallow marks. When
    /// not given, nodes with inline content default to allowing all
    /// marks, other nodes default to not allowing marks.</summary>
    public string? Marks { get; init; }

    /// <summary>The group or space-separated groups to which this node belongs,
    /// which can be referred to in the content expressions for the
    /// schema.</summary>
    public string? Group { get; init; }

    /// <summary>Should be set to true for inline nodes. (Implied for text nodes.)</summary>
    public bool? Inline { get; init; }

    /// <summary>Can be set to true to indicate that, though this isn't a <see cref="NodeType.IsLeaf">leaf
    /// node</see>, it doesn't have directly editable
    /// content and should be treated as a single unit in the view.</summary>
    public bool? Atom { get; init; }

    /// <summary>The attributes that nodes of this type get.</summary>
    public Dictionary<string, AttributeSpec>? Attrs { get; init; }

    /// <summary>Controls whether nodes of this type can be selected as a <see href="https://prosemirror.net/docs/ref/#state.NodeSelection">node
    /// selection</see>. Defaults to true for non-text
    /// nodes.</summary>
    public bool? Selectable { get; init; }

    /// <summary>Determines whether nodes of this type can be dragged without
    /// being selected. Defaults to false.</summary>
    public bool? Draggable { get; init; }

    /// <summary>Can be used to indicate that this node contains code, which
    /// causes some commands to behave differently.</summary>
    public bool? Code { get; init; }

    /// <summary>Controls way whitespace in this a node is parsed. The default is
    /// <c>"normal"</c>, which causes the
    /// <see href="https://prosemirror.net/docs/ref/#model.DOMParser">DOM parser</see> to
    /// collapse whitespace in normal mode, and normalize it (replacing
    /// newlines and such with spaces) otherwise. <c>"pre"</c> causes the
    /// parser to preserve spaces inside the node. When this option isn't
    /// given, but [`code`](#model.NodeSpec.code) is true, <c>whitespace</c>
    /// will default to <c>"pre"</c>. Note that this option doesn't influence
    /// the way the node is rendered—that should be handled by <c>toDOM</c>
    /// and/or styling.</summary>
    public WhiteSpace? Whitespace { get; init; }

    /// <summary>Determines whether this node is considered an important parent
    /// node during replace operations (such as paste). Non-defining (the
    /// default) nodes get dropped when their entire content is replaced,
    /// whereas defining nodes persist and wrap the inserted content.</summary>
    public bool? DefiningAsContext { get; init; }

    /// <summary>In inserted content the defining parents of the content are
    /// preserved when possible. Typically, non-default-paragraph
    /// textblock types, and possibly list items, are marked as defining.</summary>
    public bool? DefiningForContent { get; init; }

    /// <summary>When enabled, enables both
    /// <see cref="DefiningAsContext"><c>definingAsContext</c></see> and
    /// <see cref="DefiningForContent"><c>DefiningForContent</c></see>.</summary>
    public bool? Defining { get; init; }

    /// <summary>When enabled (default is false), the sides of nodes of this type
    /// count as boundaries that regular editing operations, like
    /// backspacing or lifting, won't cross. An example of a node that
    /// should probably have this enabled is a table cell.</summary>
    public bool? Isolating { get; init; }

    /// <summary>Defines the default way a node of this type should be serialized
    /// to a string representation for debugging (e.g. in error messages).</summary>
    public Func<Node, string>? ToDebugString { get; init; }

    /// <summary>Defines the default way a <see cref="NodeType.IsLeaf">leaf node</see> of
    /// this type should be serialized to a string (as used by
    /// <see cref="Node.TextBetween(int, int, string?, Func{Node, String}?)"/> and
    /// <see cref="Node.TextContent"/>.</summary>
    public Func<Node, string>? LeafText { get; init; }
}

/// <summary>Used to define marks when creating a schema.</summary>
public class MarkSpec {
    /// <summary>The attributes that marks of this type get.</summary>
    public Dictionary<string, AttributeSpec>? Attrs { get; init; }

    /// <summary>Whether this mark should be active when the cursor is positioned
    /// at its end (or at its start when that is also the start of the
    /// parent node). Defaults to true.</summary>
    public bool? Inclusive { get; init; }

    /// <summary>Determines which other marks this mark can coexist with. Should
    /// be a space-separated strings naming other marks or groups of marks.
    /// When a mark is <see cref="Mark.AddToSet(List{Mark})">added</see> to a set, all marks
    /// that it excludes are removed in the process. If the set contains
    /// any mark that excludes the new mark but is not, itself, excluded
    /// by the new mark, the mark can not be added an the set. You can
    /// use the value <c>"_"</c> to indicate that the mark excludes all
    /// marks in the schema.
    /// <br/><br/>
    /// Defaults to only being exclusive with marks of the same type. You
    /// can set it to an empty string (or any string not containing the
    /// mark's own name) to allow multiple marks of a given type to
    /// coexist (as long as they have different attributes).</summary>
    public string? Excludes { get; init; }

    /// <summary>The group or space-separated groups to which this mark belongs.</summary>
    public string? Group { get; init; }

    /// <summary>Determines whether marks of this type can span multiple adjacent
    /// nodes when serialized to DOM/HTML. Defaults to true.</summary>
    public bool? Spanning { get; init; }
}

/// <summary>Used to <see cref="NodeSpec.Attrs">define</see> attributes on nodes or
/// marks.</summary>
public class AttributeSpec {
    /// <summary>The default value for this attribute, to use when no explicit
    /// value is provided. Attributes that have no default must be
    /// provided whenever a node or mark of a type that has them is
    /// created.</summary>
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<JsonNode> Default { get; init; }
}

/// <summary>A document schema. Holds <see cref="NodeType">node</see> and
/// <see cref="MarkType">mark type</see> objects for the nodes and marks that may
/// occur in conforming documents, and provides functionality for
/// creating and deserializing such documents.
/// <br/><br/>
/// When given, the type parameters provide the names of the nodes and
/// marks in this schema.</summary>
public class Schema {
    /// <summary>The <see cref="SchemaSpec"/> on which the schema is based,
    /// with the added guarantee that its <c>nodes</c> and <c>marks</c>
    /// properties are <see cref="OrderedDictionary{String, NodeSpec}"/> instances.</summary>
    public ActiveSchemaSpec Spec { get; init; }

    /// <summary>An object mapping the schema's node names to node type objects.</summary>
    public Dictionary<string, NodeType> Nodes{ get; init; }

    /// <summary>A map from mark names to mark type objects.</summary>
    public Dictionary<string, MarkType> Marks { get; init; }

    public class ActiveSchemaSpec : ISchemaSpec {
        public required OrderedDictionary<string, NodeSpec> Nodes { get; init; }
        public required OrderedDictionary<string, MarkSpec> Marks { get; init; }
        public string? TopNode { get; init; }
    }

    /// <summary>Construct a schema from a schema <see cref="SchemaSpec">specification</see>.</summary>
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

    /// <summary>The type of the <see cref="SchemaSpec.TopNode">default top node</see>
    /// for this schema.</summary>
    public NodeType TopNodeType { get; init; }

    /// <summary>Create a node in this schema. The <c>type</c> may be a string or a
    /// <c>NodeType</c> instance. Attributes will be extended with defaults,
    /// <c>content</c> may be a <c>Fragment</c>, <c>null</c>, a <c>Node</c>, or an array of
    /// nodes.</summary>
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

    /// <summary>Create a text node in the schema. Empty text nodes are not
    /// allowed.</summary>
    public TextNode Text(string text, List<Mark>? marks = null) {
        var type = Nodes["text"];
        return new TextNode(type, type.DefaultAttrs, text, Model.Mark.SetFrom(marks!));
    }

    /// <summary>Create a mark with the given type and attributes.</summary>
    public Mark Mark(OneOf<string, MarkType> type, Attrs? attrs = null) {
        var _type = type.Match(
            str => Marks[str],
            markType => markType
        );
        return _type.Create(attrs);
    }

    /// <summary>Deserialize a node from its JSON representation.</summary>
    public Node NodeFromJSON(NodeDto json) {
        return Model.Node.FromJSON(this, json);
    }

    /// <summary>Deserialize a mark from its JSON representation.</summary>
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