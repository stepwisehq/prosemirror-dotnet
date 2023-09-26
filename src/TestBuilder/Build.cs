using System.Dynamic;
using System.Text.Json.Nodes;

using OneOf;
using DotNext;

using StepWise.Prose.Collections;
using StepWise.Prose.Model;
using StepWise.Prose.SchemaBasic;
using System.Text.RegularExpressions;


namespace StepWise.Prose.TestBuilder;

using ChildSpec = OneOf<string, Node, MarkFlat>;
using ChildSpec2 = OneOf<string, Node, List<Node>, MarkFlat, object>;
using Tags = Dictionary<string, int>;


public class MarkFlat {
    public List<Node> Flat { get; }

    public MarkFlat(List<Node> flat, Tags tag) {
        Flat = flat;
        this.GetUserData().Set(Builder.TagSlot, tag);
    }
}

public static class BuilderNodeExtensions {
    public static Tags Tag(this Node node) =>
        node.GetUserData().TryGet(Builder.TagSlot, out var tag) && tag is Tags tags ? tags : Builder.NoTag;
}

public static class Builder {
    private static AsyncLocal<Schema> _schema { get; set; } = new();
    public static Schema schema {
        get {
            if (_schema.Value is null) _schema.Value = BasicSchema.Schema;
            return _schema.Value!;
        } set => _schema.Value = value;
    }
    public static Tags NoTag {get;} = new();
    public static readonly UserDataSlot<Tags> TagSlot = new();

    public static Node doc(params ChildSpec[] nodes) => doc(new(), nodes);
    public static Node doc(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("doc", new(), attrs, nodes.ToList());

    public static Node p(params ChildSpec[] nodes) => p(new(), nodes);
    public static Node p(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("paragraph", new(), attrs, nodes.ToList());

    public static Node hr() => CreateBlock("horizontal_rule", new(), new(), new());

    public static Node br() => CreateBlock("hard_break", new(), new(), new());

    public static MarkFlat a(params ChildSpec[] nodes) => a(new(), nodes);
    public static MarkFlat a(object attrs, params ChildSpec[] nodes) {
        Attrs defaultAttrs = new() { ["href"] = "foo" };
        return CreateMark("link", defaultAttrs, CreateAttrs(attrs), nodes.ToList());
    }

    public static Node h1(params ChildSpec[] nodes) => h1(new(), nodes);
    public static Node h1(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("heading", new() {["level"] = 1}, attrs, nodes.ToList());

    public static Node h2(params ChildSpec[] nodes) => h2(new(), nodes);
    public static Node h2(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("heading", new() {["level"] = 2}, attrs, nodes.ToList());

    public static Node h3(params ChildSpec[] nodes) => h3(new(), nodes);
    public static Node h3(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("heading", new() {["level"] = 3}, attrs, nodes.ToList());

    public static Node li(params ChildSpec[] nodes) => li(new(), nodes);
    public static Node li(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("list_item", new() {}, attrs, nodes.ToList());

    public static Node ul(params ChildSpec[] nodes) => ul(new(), nodes);
    public static Node ul(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("bullet_list", new() {}, attrs, nodes.ToList());

    public static Node ol(params ChildSpec[] nodes) => ol(new(), nodes);
    public static Node ol(Attrs attrs, params ChildSpec[] nodes) =>
        CreateBlock("ordered_list", new() {}, attrs, nodes.ToList());

    public static Node img(params ChildSpec[] nodes) => img(new(), nodes);
    public static Node img(object attrs, params ChildSpec[] nodes) =>
        CreateBlock("image", new() {["src"] = "image.png"}, CreateAttrs(attrs), nodes.ToList());

    public static Node pre(params ChildSpec[] nodes) => pre(new(), nodes);
    public static Node pre(object attrs, params ChildSpec[] nodes) =>
        CreateBlock("code_block", new(), CreateAttrs(attrs), nodes.ToList());

    public static Node blockquote(params ChildSpec[] nodes) => blockquote(new(), nodes);
    public static Node blockquote(object attrs, params ChildSpec[] nodes) =>
        CreateBlock("blockquote", new(), CreateAttrs(attrs), nodes.ToList());

    public static MarkFlat em(params ChildSpec[] nodes) =>
        CreateMark("em", new(), new(), nodes.ToList());

    public static MarkFlat strong(params ChildSpec[] nodes) =>
        CreateMark("strong", new(), new(), nodes.ToList());

    public static MarkFlat code(params ChildSpec[] nodes) =>
        CreateMark("code", new(), new(), nodes.ToList());

    private static Attrs CreateAttrs(object obj) {
        var attrs = new Attrs();
        var props = obj.GetType().GetProperties();

        foreach (var prop in props) {
            var value = prop.GetValue(obj, null);
            var name = prop.Name;
            attrs[name] = value switch {
                null => null,
                string str => str,
                int i => i,
                bool b => b,
                double d => d,
                JsonNode jn => jn,
                _ => throw new Exception($"Unknown attr type for property {name}")
            };
        }
        return attrs;
    }

    private static Node CreateBlock(string type, Attrs defaultAttrs, Attrs suppliedAttrs, List<ChildSpec> children) {
        var pType = schema.NodeType(type);
        var attrs = new Attrs(defaultAttrs);
        foreach (var (name, value) in suppliedAttrs) attrs[name] = value;
        var (nodes, tag) = Flatten(schema, children, n => n);
        var node = pType.Create(attrs, nodes);
        if (!ReferenceEquals(tag, NoTag)) node.GetUserData().Set(TagSlot, tag);
        return node;
    }

    private static MarkFlat CreateMark(string type, Attrs defaultAttrs, Attrs suppliedAttrs, List<ChildSpec> children) {
        var mTypes = schema.Marks[type]!;
        var attrs = new Attrs(defaultAttrs);
        foreach (var (name, value) in suppliedAttrs) attrs[name] = value;
        var mark = mTypes.Create(attrs);
        var (nodes, tag) = Flatten(schema, children, n => {
            var newMarks = mark.AddToSet(n.Marks);
            return newMarks.Count > n.Marks.Count ? n.Mark(newMarks) : n;
        });
        return new MarkFlat(nodes, tag);
    }

    private static (List<Node>, Tags) Flatten(Schema schema, List<ChildSpec> children, Func<Node, Node> f) {
        var result = new List<Node>();
        var pos = 0;
        var tag = NoTag;

        for (var i = 0; i < children.Count; i ++) {
            var child = children[i];
            if (child.Value is string strChild) {
                var re = new Regex(@"<(\w+)>");
                var matches = re.Matches(strChild);
                var outText = "";
                var at = 0;
                foreach (var match in matches.ToList()) {
                    outText += strChild[at..match.Index];
                    pos += match.Index - at;
                    at = match.Index + match.Length;
                    if (ReferenceEquals(tag, NoTag)) tag = new();
                    tag[match.Groups[1].Value] = pos;
                }
                outText += strChild[at..];
                pos += strChild.Length - at;
                if (outText.Length > 0) result.Add(f(schema.Text(outText)));
            } else {
                if (child.Value.GetUserData().TryGet(TagSlot, out var childTag) && !ReferenceEquals(childTag, NoTag)) {
                    if (ReferenceEquals(tag, NoTag)) tag = new();
                    foreach (var (name, value) in childTag)
                        tag[name] = value
                        + (child.Value is MarkFlat || (child.Value is Node childNode && childNode.IsText) ? 0 : 1)
                        + pos;
                }
                if (child.Value is MarkFlat markFlat) {
                    for (var j = 0; j < markFlat.Flat.Count; j++) {
                        var node = f(markFlat.Flat[j]);
                        pos += node.NodeSize;
                        result.Add(node);
                    }
                } else if (child.Value is Node childNode) {
                    var node = f(childNode);
                    pos += node.NodeSize;
                    result.Add(node);
                } else throw new Exception("Unknown childspec type");
            }
        }
        return (result, tag);
    }


    public delegate ChildSpec ChildBuilder(params ChildSpec2[] nodes);

    public delegate MarkFlat MarkBuilder(params ChildSpec2[] nodes);
    public delegate Node NodeBuilder(params ChildSpec2[] nodes);


    public static dynamic BuildersDynamic(Schema schema, Dictionary<string, object> spec) {
        dynamic builders = new ExpandoObject();
        var (nodes, marks) = Builders(schema, spec);

        foreach (var (name, builder) in nodes) ((IDictionary<string, object>)builders).Add(name, builder);
        foreach (var (name, builder) in marks) ((IDictionary<string, object>)builders).Add(name, builder);

        return builders;
    }
    public static (Dictionary<string, NodeBuilder> nodes, Dictionary<string, MarkBuilder> marks) Builders(Schema schema, Dictionary<string, object> spec) {
        var marks = new Dictionary<string, MarkBuilder>();
        var nodes = new Dictionary<string, NodeBuilder>();

        foreach(var (name, _) in schema.Nodes) nodes[name] = MakeBlockBuilder(schema, name, new());
        foreach(var (name, _) in schema.Marks) marks[name] = MakeMarkBuilder(schema, name, new());

        foreach (var (name, attrsObj) in spec) {
            var defaultAttrs = CreateAttrs(attrsObj);
            if (!defaultAttrs.TryGetValue("nodeType", out var jType)) jType = defaultAttrs["markType"];
            var type = (string)jType!;
            if (schema.Marks.ContainsKey(type)) marks[name] = MakeMarkBuilder(schema, type, defaultAttrs);
            else if (schema.Nodes.ContainsKey(type)) nodes[name] = MakeBlockBuilder(schema, type, defaultAttrs);
            else throw new Exception($"Unknown type {type}");
        }

        return (nodes, marks);
    }

    private static MarkBuilder MakeMarkBuilder(Schema schema, string type, Attrs defaultAttrs) {
        MarkFlat builder(params ChildSpec2[] children) {
            Attrs suppliedAttrs = new();
            List<ChildSpec> _children = new();

            foreach (var node in children) {
                node.Switch(
                    str => _children.Add(str),
                    node => _children.Add(node),
                    list => list.ForEach(n => _children.Add(n)),
                    flat => _children.Add(flat),
                    obj => suppliedAttrs = CreateAttrs(obj)
                );
            }

            var mTypes = schema.Marks[type!]!;
            var attrs = new Attrs(defaultAttrs ?? new());
            foreach (var (name, value) in suppliedAttrs) attrs[name] = value;
            var mark = mTypes.Create(attrs);
            var (nodes, tag) = Flatten(schema, _children, n => {
                var newMarks = mark.AddToSet(n.Marks);
                return newMarks.Count > n.Marks.Count ? n.Mark(newMarks) : n;
            });
            return new MarkFlat(nodes, tag);
        };
        return builder;
    }

    private static NodeBuilder MakeBlockBuilder(Schema schema, string type, Attrs defaultAttrs) {
        Node builder(params ChildSpec2[] children) {
            Attrs suppliedAttrs = new();
            List<ChildSpec> _children = new();

            foreach (var _node in children) {
                _node.Switch(
                    str => _children.Add(str),
                    node => _children.Add(node),
                    list => list.ForEach(n => _children.Add(n)),
                    flat => _children.Add(flat),
                    obj => suppliedAttrs = CreateAttrs(obj)
                );
            }

            var pType = schema.NodeType(type);
            var attrs = new Attrs(defaultAttrs ?? new());
            foreach (var (name, value) in suppliedAttrs) attrs[name] = value;
            var (nodes, tag) = Flatten(schema, _children, n => n);
            var node = pType.Create(attrs, nodes);
            if (!ReferenceEquals(tag, NoTag)) node.GetUserData().Set(TagSlot, tag);
            return node;
        }
        return builder;
    }
}

public static class OrderedDictionaryExtensions {
    public static OrderedDictionary<TKey,V> Append<TKey,V>(this OrderedDictionary<TKey,V> dict, TKey key, V value) where TKey : notnull {
        dict.Remove(key);
        dict.Add(key, value);
        return dict;
    }
}