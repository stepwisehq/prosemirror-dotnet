using System.Text.Json;

using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.TestBuilder;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class NodeTest {
    public ITestOutputHelper Out;

    public NodeTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static Schema customSchema = new Schema(new() {
        Nodes = new() {
            ["doc"] = new() {Content = "paragraph+"},
            ["paragraph"] = new() {Content = "(text|contact)*"},
            ["text"] = new() { ToDebugString = (node) => "custom_text"},
            ["contact"] = new() {
                Inline = true,
                Attrs = new() { ["name"] = new(), ["email"] = new() },
                LeafText = (node) => $"{node.Attrs["name"]} <{node.Attrs["email"]}>",
            },
            ["hard_break"] = new() { ToDebugString = (node) => "custom_hard_break" },
        }
    });

    [Fact] public void Nests() =>
      ist(doc(ul(li(p("hey"), p()), li(p("foo")))).ToString(),
          """doc(bullet_list(list_item(paragraph("hey"), paragraph), list_item(paragraph("foo"))))""");

    [Fact] public void Shows_Ineline_Children() =>
      ist(doc(p("foo", img(), br(), "bar")).ToString(),
          """doc(paragraph("foo", image, hard_break, "bar"))""");

    [Fact] public void Shows_Marks() =>
      ist(doc(p("foo", em("bar", strong("quux")), code("baz"))).ToString(),
          """doc(paragraph("foo", em("bar"), em(strong("quux")), code("baz")))""");

    private static void cut(Node doc, Node cut) {
        int? tagB = doc.Tag().ContainsKey("b") ? doc.Tag()["b"] : null;
        ist(doc.Cut(doc.Tag().GetValueOrDefault("a"), tagB), cut, eq);
    }

   [Fact] public void Extracts_A_Full_Block() =>
       cut(doc(p("foo"),"<a>",p("bar"),"<b>",p("baz")),
           doc(p("bar")));

   [Fact] public void Cuts_Text() =>
       cut(doc(p("0"),p("foo<a>bar<b>baz"),p("2")),
           doc(p("bar")));

   [Fact] public void Cuts_Deeply() =>
       cut(doc(blockquote(ul(li(p("a"),p("b<a>c")),li(p("d")),"<b>",li(p("e"))),p("3"))),
           doc(blockquote(ul(li(p("c")),li(p("d"))))));

   [Fact] public void Works_From_The_Left() =>
       cut(doc(blockquote(p("foo<b>bar"))),
           doc(blockquote(p("foo"))));

   [Fact] public void Works_To_The_Right() =>
       cut(doc(blockquote(p("foo<a>bar"))),
           doc(blockquote(p("bar"))));

   [Fact] public void Preserves_Marks() =>
       cut(doc(p("foo",em("ba<a>r",img(),strong("baz"),br()),"qu<b>ux",code("xyz"))),
           doc(p(em("r",img(),strong("baz"),br()),"qu")));

    private static void between(Node doc, params string[] nodes) {
        var i = 0;
        doc.NodesBetween(doc.Tag()["a"], doc.Tag()["b"], (node, pos, _, _) => {
            if (i == nodes.Length)
                throw new Exception($"More nodes iterated than listed ({node.Type.Name})");
            var compare = node is TextNode textNode ? textNode.Text : node.Type.Name;
            if (compare != nodes[i++])
                throw new Exception($"Expected {JsonSerializer.Serialize(nodes[i - 1])} , got {JsonSerializer.Serialize(compare)}");
            if (!node.IsText && doc.NodeAt(pos) != node)
                throw new Exception($"Pos {pos} does not point at node {node} {doc.NodeAt(pos)}");
            return true;
        });
    }

   [Fact] public void Iterates_Over_Text() =>
       between(doc(p("foo<a>bar<b>baz")),
               "paragraph","foobarbaz");

   [Fact] public void Descends_Multiple_Levels() =>
       between(doc(blockquote(ul(li(p("f<a>oo")),p("b"),"<b>"),p("c"))),
               "blockquote","bullet_list","list_item","paragraph","foo","paragraph","b");

   [Fact] public void Iterates_Over_Inline_Nodes() =>
       between(doc(p(em("x"),"f<a>oo",em("bar",img(),strong("baz"),br()),"quux",code("xy<b>z"))),
               "paragraph","foo","bar","image","baz","hard_break","quux","xyz");


   [Fact] public void Works_When_Passing_A_Custom_Function_As_LeafText() {
      var d = doc(p("foo", img(), br()));
      ist(d.TextBetween(0, d.Content.Size, "", (node) => {
        if (node.Type.Name == "image") return "<image>";
        if (node.Type.Name == "hard_break") return "<break>";
        return "";
      }), "foo<image><break>");
   }

   [Fact] public void Works_With_LeafText() {
      var d = customSchema.Nodes["doc"].CreateChecked(new(), new NodeList {
        customSchema.Nodes["paragraph"].CreateChecked(new(), new NodeList {
          customSchema.Text("Hello "),
          customSchema.Nodes["contact"].CreateChecked(new() {["name"] = "Alice", ["email"] = "alice@example.com" })
        })
      });
      ist(d.TextBetween(0, d.Content.Size), "Hello Alice <alice@example.com>");
    }

   [Fact] public void Should_Ignore_LeafText_When_Passing_A_Custom_LeafText() {
      var d = customSchema.Nodes["doc"].CreateChecked(new(), new NodeList {
        customSchema.Nodes["paragraph"].CreateChecked(new(), new NodeList {
          customSchema.Text("Hello "),
          customSchema.Nodes["contact"].CreateChecked(new() {["name"] = "Alice", ["email"] = "alice@example.com" })
        })
      });
      ist(d.TextBetween(0,d.Content.Size, "", "<anonymous>"), "Hello <anonymous>");
    }


   [Fact] public void Works_On_A_Whole_Doc() =>
      ist(doc(p("foo")).TextContent,"foo");

   [Fact] public void Works_On_A_Text_Node() =>
      ist(schema.Text("foo").TextContent,"foo");

   [Fact] public void Works_On_A_Nested_Element() =>
      ist(doc(ul(li(p("hi")),li(p(em("a"),"b")))).TextContent,
          "hiab");

    private static void from(IContentLike? arg, Node expect) =>
        ist(expect.Copy(Fragment.From(arg)), expect, eq);

   [Fact] public void Wraps_A_Single_Node() =>
      from(schema.Node("paragraph"), doc(p()));

   [Fact] public void Wraps_An_Array() =>
      from(new NodeList {schema.Node("hard_break"), schema.Text("foo")}, p(br(),"foo"));

   [Fact] public void Preserves_A_Fragment() =>
      from(doc(p("foo")).Content, doc(p("foo")));

   [Fact] public void Accepts_Null() =>
      from(null,p());

   [Fact] public void Joins_Adjacent_Text() =>
      from(new NodeList {schema.Text("a"),schema.Text("b")}, p("ab"));

    private static void roundTrip(Node doc) =>
        ist(schema.NodeFromJSON(doc.ToJSON()), doc, eq);

   [Fact] public void Can_Serialize_A_Simple_Node() => roundTrip(doc(p("foo")));

   [Fact] public void Can_Serialize_Marks() => roundTrip(doc(p("foo",em("bar",strong("baz"))," ",a("x"))));

   [Fact] public void Can_Serialize_Inline_Leaf_Nodes() => roundTrip(doc(p("foo",em(img(),"bar"))));

   [Fact] public void Can_Serialize_Block_Leaf_Nodes() => roundTrip(doc(p("a"),hr(),p("b"),p()));

   [Fact] public void Can_Serialize_Nested_Nodes() => roundTrip(doc(blockquote(ul(li(p("a"),p("b")),li(p(img()))),p("c")),p("d")));


   [Fact] public void Should_Have_The_Default_ToString_Method_text() => ist(schema.Text("hello").ToString(),"\"hello\"");
   [Fact] public void Should_Have_The_Default_ToString_Method_br() => ist(br().ToString(),"hard_break");

   [Fact] public void Should_Be_Able_To_Redefine_It_From_NodeSpec_By_Specifying_ToDebugString_Method() =>
    ist(customSchema.Text("hello").ToString(),"custom_text");

   [Fact] public void Should_Be_Respected_By_Fragment() =>
      ist(Fragment.FromArray(
          new() {customSchema.Text("hello"),customSchema.Nodes["hard_break"].CreateChecked(),
          customSchema.Text("world")}
        ).ToString(),
        "<custom_text, custom_hard_break, custom_text>");

   [Fact] public void Should_Custom_The_TextContent_Of_A_Leaf_Node() {
      var contact = customSchema.Nodes["contact"].CreateChecked(new() {["name"] = "Bob", ["email"] = "bob@example.com" });
      var paragraph = customSchema.Nodes["paragraph"].CreateChecked(new(), new NodeList {customSchema.Text("Hello "), contact});

      ist(contact.TextContent,"Bob <bob@example.com>");
      ist(paragraph.TextContent,"Hello Bob <bob@example.com>");
    }
}