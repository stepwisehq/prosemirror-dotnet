using StepWise.Prose.Collections;
using StepWise.Prose.Model;
using StepWise.Prose.SchemaList;


namespace StepWise.Prose.SchemaBasic;

// const pDOM: DOMOutputSpec = ["p", 0], blockquoteDOM: DOMOutputSpec = ["blockquote", 0],
//             hrDOM: DOMOutputSpec = ["hr"], preDOM: DOMOutputSpec = ["pre", ["code", 0]],
//             brDOM: DOMOutputSpec = ["br"]

public static class BasicSchema {
    /// [Specs](#model.NodeSpec) for the nodes defined in this schema.
    public static OrderedDictionary<string, NodeSpec> Nodes { get; } = new() {
        // NodeSpec The top level document node.
        ["doc"] =  new() {
            Content = "block+"
        },

        // A plain paragraph textblock. Represented in the DOM
        // as a `<p>` element.
        ["paragraph"] =  new() {
            Content = "inline*",
            Group = "block",
            // ParseDOM = [ new() {tag = "p"}],
            // ToDOM()  new() { return pDOM }
        },

        // A blockquote (`<blockquote>`) wrapping one or more blocks.
        ["blockquote"] =  new() {
            Content = "block+",
            Group = "block",
            Defining = true,
            // ParseDOM = [ new() {tag = "blockquote"}],
            // ToDOM()  new() { return blockquoteDOM }
        },

        // A horizontal rule (`<hr>`).
        ["horizontal_rule"] =  new() {
            Group = "block",
            // ParseDOM = [ new() {tag = "hr"}],
            // toDOM()  new() { return hrDOM }
        },

        // A heading textblock, with a `level` attribute that
        // should hold the number 1 to 6. Parsed and serialized as `<h1>` to
        // `<h6>` elements.
        ["heading"] =  new() {
            Attrs =  new() {["level"] =  new() {Default = new(1)}},
            Content = "inline*",
            Group = "block",
            Defining = true,
            // ParseDOM = [ new() {tag = "h1", attrs =  new() {level = 1}},
            //                       new() {tag = "h2", attrs =  new() {level = 2}},
            //                       new() {tag = "h3", attrs =  new() {level = 3}},
            //                       new() {tag = "h4", attrs =  new() {level = 4}},
            //                       new() {tag = "h5", attrs =  new() {level = 5}},
            //                       new() {tag = "h6", attrs =  new() {level = 6}}],
            // toDOM(node)  new() { return ["h" + node.attrs.level, 0] }
        },

        // A code listing. Disallows marks or non-text inline
        // nodes by default. Represented as a `<pre>` element with a
        // `<code>` element inside of it.
        ["code_block"] =  new() {
            Content = "text*",
            Marks = "",
            Group = "block",
            Code = true,
            Defining = true,
            // ParseDOM = [ new() {tag = "pre", preserveWhitespace = "full"}],
            // toDOM()  new() { return preDOM }
        },

        // The text node.
        ["text"] =  new() {
            Group = "inline"
        },

        // An inline image (`<img>`) node. Supports `src`,
        // `alt`, and `href` attributes. The latter two default to the empty
        // string.
        ["image"] =  new() {
            Inline = true,
            Attrs =  new() {
                ["src"] =  new() {},
                ["alt"] =  new() {Default = null},
                ["title"] =  new() {Default = null}
            },
            Group = "inline",
            Draggable = true,
            // ParseDOM = [ new() {tag = "img[src]", getAttrs(dom = HTMLElement)  new() {
            //     return  new() {
            //         src = dom.getAttribute("src"),
            //         title = dom.getAttribute("title"),
            //         alt = dom.getAttribute("alt")
            //     }
            // }}],
            // toDOM(node)  new() { let  new() {src, alt, title} = node.attrs; return ["img",  new() {src, alt, title}] }
        },

        // A hard line break, represented in the DOM as `<br>`.
        ["hard_break"] =  new() {
            Inline = true,
            Group = "inline",
            Selectable = false,
            // ParseDOM = [ new() {tag = "br"}],
            // toDOM()  new() { return brDOM }
        }
    };

    // const emDOM: DOMOutputSpec = ["em", 0], strongDOM: DOMOutputSpec = ["strong", 0], codeDOM: DOMOutputSpec = ["code", 0]

    /// [Specs](#model.MarkSpec) for the marks in the schema.
    public static OrderedDictionary<string, MarkSpec> Marks { get; } = new() {
        // A link. Has `href` and `title` attributes. `title`
        // defaults to the empty string. Rendered and parsed as an `<a>`
        // element.
        ["link"] = new() {
            Attrs = new() {
                ["href"] = new() {},
                ["title"] = new() {Default = null}
            },
            Inclusive = false,
            // parseDOM = [new() {tag = "a[href]", getAttrs(dom = HTMLElement) new() {
            //     return new() {href = dom.getAttribute("href"), title = dom.getAttribute("title")}
            // }}],
            // toDOM(node) new() { let new() {href, title} = node.attrs; return ["a", new() {href, title}, 0] }
        },

        // An emphasis mark. Rendered as an `<em>` element. Has parse rules
        // that also match `<i>` and `font-style = italic`.
        ["em"] = new() {
            // parseDOM = [
            //     new() {tag = "i"}, new() {tag = "em"},
            //     new() {style = "font-style=italic"},
            //     new() {style = "font-style=normal", clearMark = m => m.type.name == "em"}
            // ],
            // toDOM() new() { return emDOM }
        },

        // A strong mark. Rendered as `<strong>`, parse rules also match
        // `<b>` and `font-weight = bold`.
        ["strong"] = new() {
            // parseDOM = [
            //     new() {tag = "strong"},
            //     // This works around a Google Docs misbehavior where
            //     // pasted content will be inexplicably wrapped in `<b>`
            //     // tags with a font-weight normal.
            //     new() {tag = "b", getAttrs = (node = HTMLElement) => node.style.fontWeight != "normal" && null},
            //     new() {style = "font-weight=400", clearMark = m => m.type.name == "strong"},
            //     new() {style = "font-weight", getAttrs = (value = string) => /^(bold(er)?|[5-9]\dnew() {2,})$/.test(value) && null},
            // ],
            // toDOM() new() { return strongDOM }
        },

        // Code font mark. Represented as a `<code>` element.
        ["code"] = new() {
            // parseDOM = [new() {tag = "code"}],
            // toDOM() new() { return codeDOM }
        }
    };

    public static Schema Schema { get; } = new Schema(new() {
        Nodes = ListSchema.AddListNodes(Nodes, "paragraph block*", "block"),
        Marks = Marks
    });
}

// This schema roughly corresponds to the document schema used by
// [CommonMark](http://commonmark.org/), minus the list elements,
// which are defined in the [`prosemirror-schema-list`](#schema-list)
// module.
//
// To reuse elements from this schema, extend or read from its
// `spec.nodes` and `spec.marks` [properties](#model.Schema.spec).
// export const schema = new Schema({nodes, marks})