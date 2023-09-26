using System.Text.Json.Nodes;

using OneOf;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Collections;
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using DotNext.Collections.Generic;


namespace StepWise.Prose.Transformation.Test;

using static Builder;
using static StepWise.Prose.Test.TestUtils;

public class TransformTest {
    public ITestOutputHelper Out;

    public TransformTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static int tag(Node node, string tag) {
        return node.Tag()[tag];
    }

    private static int tag(Node node, string tagA, string tagB) {
        if (node.Tag().ContainsKey(tagA)) return node.Tag()[tagA];
        return node.Tag()[tagB];
    }

    private static void add(Node doc, Model.Mark mark, Node expect) {
        Util.testTransform(new Transform(doc).AddMark(tag(doc, "a"), tag(doc, "b"), mark), expect);
    }

   [Fact] public void Should_Add_A_Mark() =>
       add(doc(p("hello <a>there<b>!")),
          schema.Mark("strong"),
          doc(p("hello ",strong("there"),"!")));

   [Fact] public void Should_Only_Add_A_Mark_Once() =>
       add(doc(p("hello ",strong("<a>there"),"!<b>")),
          schema.Mark("strong"),
          doc(p("hello ",strong("there!"))));

   [Fact] public void Should_Join_Overlapping_Marks() =>
       add(doc(p("one <a>two ",em("three<b>_Four"))),
          schema.Mark("strong"),
          doc(p("one ",strong("two ",em("three")),em("_Four"))));

   [Fact] public void Should_Overwrite_Marks_With_Different_Attributes() =>
       add(doc(p("this is a ",a("<a>link<b>"))),
          schema.Mark("link",new() {["href"] = "bar"}),
          doc(p("this is a ",a(new {href = "bar"}, "link"))));

   [Fact] public void Can_Add_A_Mark_In_A_Nested_Node() =>
       add(doc(p("before"),blockquote(p("the_Variable_Is_Called <a>i<b>")),p("after")),
          schema.Mark("code"),
          doc(p("before"),blockquote(p("the_Variable_Is_Called ",code("i"))),p("after")));

   [Fact] public void Can_Add_A_Mark_Across_Blocks() =>
       add(doc(p("hi <a>this"),blockquote(p("is")),p("a docu<b>ment"),p("!")),
          schema.Mark("em"),
          doc(p("hi ",em("this")),blockquote(p(em("is"))),p(em("a docu"),"ment"),p("!")));

   [Fact] public void Does_Not_Remove_Non_Excluded_Marks_Of_The_Same_Type() {
     var schema =new Schema(new() {
       Nodes = new() {["doc"] = new() {Content = "text*"},
                      ["text"] = new() {}},
       Marks = new() {["comment"] = new() {Excludes = "", Attrs = new() {["id"] = new(){}}}}
     });
     var tr = new Transform(schema.Node("doc",null,schema.Text("hi",new(){schema.Mark("comment",new(){["id"] = 10})})));
     tr.AddMark(0, 2, schema.Mark("comment", new() {["id"] = 20}));
     ist(tr.Doc.FirstChild!.Marks.Count,2);
    }

   [Fact] public void Can_Remove_Multiple_Excluded_Marks() {
     var schema =new Schema(new() {
       Nodes = new() {["doc"] = new() {Content = "text*"},
                      ["text"] = new()},
       Marks = new() {["big"] = new() {Excludes = "small1 small2"},
                      ["small1"] = new(), ["small2"] = new()}
    });
     var tr =new Transform(schema.Node("doc",null,schema.Text("hi",new(){schema.Mark("small1"),schema.Mark("small2")})));
     ist(tr.Doc.FirstChild!.Marks.Count,2);
     tr.AddMark(0,2,schema.Mark("big"));
     ist(tr.Doc.FirstChild!.Marks.Count,1);
     ist(tr.Doc.FirstChild!.Marks[0].Type.Name,"big");
    }

    private static void rem(Node doc, Model.Mark? mark, Node expect) {
        Util.testTransform(new Transform(doc).RemoveMark(tag(doc, "a"), tag(doc, "b"), mark!), expect);
    }

   [Fact] public void Can_Cut_A_Gap() =>
      rem(doc(p(em("hello <a>world<b>!"))),
          schema.Mark("em"),
          doc(p(em("hello "),"world",em("!"))));

   [Fact] public void Doesnt_Do_Anything_When_Theres_No_Mark() =>
      rem(doc(p(em("hello")," <a>world<b>!")),
          schema.Mark("em"),
          doc(p(em("hello")," <a>world<b>!")));

   [Fact] public void Can_Remove_Marks_From_Nested_Nodes() =>
      rem(doc(p(em("one ",strong("<a>two<b>")," three"))),
          schema.Mark("strong"),
          doc(p(em("one two three"))));

   [Fact] public void Can_Remove_A_Link() =>
      rem(doc(p("<a>hello ",a("link<b>"))),
          schema.Mark("link",new () {["href"] = "foo"}),
          doc(p("hello link")));

   [Fact] public void Doesnt_Remove_A_Non_Matching_Link() =>
      rem(doc(p("<a>hello ",a("link<b>"))),
          schema.Mark("link",new() {["href"] = "bar"}),
          doc(p("hello ",a("link"))));

   [Fact] public void Can_Remove_Across_Blocks() =>
      rem(doc(blockquote(p(em("much <a>em")),p(em("here_Too"))),p("between",em("...")),p(em("end<b>"))),
          schema.Mark("em"),
          doc(blockquote(p(em("much "),"em"),p("here_Too")),p("between..."),p("end")));

   [Fact] public void Can_Remove_Everything() =>
      rem(doc(p("<a>hello,",em("this is ",strong("much")," ",a("markup<b>")))),
          null,
          doc(p("<a>hello,this is much markup")));

   [Fact] public void Can_Remove_More_Than_One_Mark_Of_The_Same_Type_From_A_Block() {
     var schema = new Schema(new() {
        Nodes = new() {["doc"] = new() {Content = "text*"},
                       ["text"] = new() {}},
        Marks = new() {["comment"] = new() {Excludes = "",Attrs = new() {["id"] = new()}}}
      });
     var tr = new Transform(schema.Node("doc",null,schema.Text("hi",new() {schema.Mark("comment",new(){["id"]= 1}),schema.Mark("comment",new(){["id"] = 2})})));
     ist(tr.Doc.FirstChild!.Marks.Count,2);
     tr.RemoveMark(0,2,schema.Marks["comment"]);
     ist(tr.Doc.FirstChild!.Marks.Count,0);
    }

    private static void ins(Node doc, Node nodes, Node expect) =>
        ins(doc, new NodeList {nodes}, expect);
    private static void ins(Node doc, NodeList nodes, Node expect) =>
        Util.testTransform(new Transform(doc).Insert(tag(doc, "a"), nodes), expect);

   [Fact] public void Can_Insert_A_Break() =>
      ins(doc(p("hello<a>there")),
          schema.Node("hard_break"),
          doc(p("hello",br(),"<a>there")));

   [Fact] public void Can_Insert_An_Empty_Paragraph_At_The_Top() =>
      ins(doc(p("one"),"<a>",p("two<2>")),
          schema.Node("paragraph"),
          doc(p("one"),p(),"<a>",p("two<2>")));

   [Fact] public void Can_Insert_Two_Block_Nodes() =>
      ins(doc(p("one"),"<a>",p("two<2>")),
           new NodeList {schema.Node("paragraph",null, schema.Text("hi")),
           schema.Node("horizontal_rule")},
          doc(p("one"),p("hi"),hr(),"<a>",p("two<2>")));

   [Fact] public void Can_Insert_At_The_End_Of_A_Blockquote() =>
      ins(doc(blockquote(p("he<before>y"),"<a>"),p("after<after>")),
          schema.Node("paragraph"),
          doc(blockquote(p("he<before>y"),p()),p("after<after>")));

   [Fact] public void Can_Insert_At_The_Start_Of_A_Blockquote() =>
      ins(doc(blockquote("<a>",p("he<1>y")),p("after<2>")),
          schema.Node("paragraph"),
          doc(blockquote(p(),"<a>",p("he<1>y")),p("after<2>")));

   [Fact] public void Will_Wrap_A_Node_With_The_Suitable_Parent() =>
      ins(doc(p("foo<a>bar")),
          schema.Nodes["list_item"].CreateAndFill()!,
          doc(p("foo"),ol(li(p())),p("bar")));

    private static void del(Node doc, Node expect) =>
        Util.testTransform(new Transform(doc).Delete(tag(doc, "a"), tag(doc, "b")), expect);

   [Fact] public void Can_Delete_A_Word() =>
       del(doc(p("<1>one"),"<a>",p("tw<2>o"),"<b>",p("<3>three")),
          doc(p("<1>one"),"<a><2>",p("<3>three")));

   [Fact] public void Preserves_Content_Constraints() =>
       del(doc(blockquote("<a>",p("hi"),"<b>"),p("x")),
          doc(blockquote(p()),p("x")));

   [Fact] public void Preserves_Positions_After_The_Range() =>
       del(doc(blockquote(p("a"),"<a>",p("b"),"<b>"),p("c<1>")),
          doc(blockquote(p("a")),p("c<1>")));

   [Fact] public void Doesnt_Join_Incompatible_Nodes() =>
       del(doc(pre("fo<a>o"),p("b<b>ar",img())),
          doc(pre("fo"),p("ar",img())));

   [Fact] public void Doesnt_Join_When_Marks_Are_Incompatible() =>
       del(doc(pre("fo<a>o"),p(em("b<b>ar"))),
          doc(pre("fo"),p(em("ar"))));

    private static void join(Node doc, Node expect) =>
        Util.testTransform(new Transform(doc).Join(tag(doc, "a")), expect);

   [Fact] public void Can_Join_Blocks() =>
       join(doc(blockquote(p("<before>a")),"<a>",blockquote(p("b")),p("after<after>")),
           doc(blockquote(p("<before>a"),"<a>",p("b")),p("after<after>")));

   [Fact] public void Can_Join_Compatible_Blocks() =>
       join(doc(h1("foo"),"<a>",p("bar")),
           doc(h1("foobar")));

   [Fact] public void Can_Join_Nested_Blocks() =>
       join(doc(blockquote(blockquote(p("a"),p("b<before>")),"<a>",blockquote(p("c"),p("d<after>")))),
           doc(blockquote(blockquote(p("a"),p("b<before>"),"<a>",p("c"),p("d<after>")))));

   [Fact] public void Can_Join_Lists() =>
       join(doc(ol(li(p("one")),li(p("two"))),"<a>",ol(li(p("three")))),
           doc(ol(li(p("one")),li(p("two")),"<a>",li(p("three")))));

   [Fact] public void Can_Join_List_Items() =>
       join(doc(ol(li(p("one")),li(p("two")),"<a>",li(p("three")))),
           doc(ol(li(p("one")),li(p("two"),"<a>",p("three")))));

   [Fact] public void Can_Join_Textblocks() =>
       join(doc(p("foo"),"<a>",p("bar")),
           doc(p("foo<a>bar")));

    private static void split(Node doc, OneOf<string, Node> expect, int? depth = null,
                              List<Wrapper?>? typesAfter = null)
    {
        expect.Switch(
            str => {
                istThrows(() => new Transform(doc).Split(tag(doc, "a"), depth, typesAfter));
            },
            node => {
                Util.testTransform(new Transform(doc).Split(tag(doc, "a"), depth, typesAfter), node);
            }
        );
    }

   [Fact] public void Can_Split_A_Textblock() =>
       split(doc(p("foo<a>bar")),
            doc(p("foo"),p("<a>bar")));

   [Fact] public void Correctly_Maps_Positions() =>
       split(doc(p("<1>a"),p("<2>foo<a>bar<3>"),p("<4>b")),
            doc(p("<1>a"),p("<2>foo"),p("<a>bar<3>"),p("<4>b")));

   [Fact] public void Can_Split_Two_Deep() =>
       split(doc(blockquote(blockquote(p("foo<a>bar"))),p("after<1>")),
            doc(blockquote(blockquote(p("foo")),blockquote(p("<a>bar"))),p("after<1>")),
            2);

   [Fact] public void Can_Split_Three_Deep() =>
       split(doc(blockquote(blockquote(p("foo<a>bar"))),p("after<1>")),
            doc(blockquote(blockquote(p("foo"))),blockquote(blockquote(p("<a>bar"))),p("after<1>")),
            3);

   [Fact] public void Can_Split_At_End() =>
       split(doc(blockquote(p("hi<a>"))),
            doc(blockquote(p("hi"),p("<a>"))));

   [Fact] public void Can_Split_At_Start() =>
       split(doc(blockquote(p("<a>hi"))),
            doc(blockquote(p(),p("<a>hi"))));

   [Fact] public void Can_Split_Inside_A_List_Item() =>
       split(doc(ol(li(p("one<1>")),li(p("two<a>three")),li(p("four<2>")))),
            doc(ol(li(p("one<1>")),li(p("two"),p("<a>three")),li(p("four<2>")))));

   [Fact] public void Can_Split_A_List_Item() =>
       split(doc(ol(li(p("one<1>")),li(p("two<a>three")),li(p("four<2>")))),
            doc(ol(li(p("one<1>")),li(p("two")),li(p("<a>three")),li(p("four<2>")))),
            2);

   [Fact] public void Respects_The_Type_Param() =>
       split(doc(h1("hell<a>o!")),
            doc(h1("hell"),p("<a>o!")),
            null,new(){new(schema.Nodes["paragraph"], null)});

   [Fact] public void Preserves_Content_Constraints_Before() =>
       split(doc(blockquote("<a>",p("x"))),"fail");

   [Fact] public void Preserves_Content_Constraints_After() =>
       split(doc(blockquote(p("x"),"<a>")),"fail");

    private static void lift(Node doc, Node expect) {
        var range = doc.Resolve(tag(doc, "a")).BlockRange(doc.Resolve(tag(doc,"b","a")));
        Util.testTransform(new Transform(doc).Lift(range!, Structure.LiftTarget(range!)!.Value), expect);
    }

   [Fact] public void Can_Lift_A_Block_Out_Of_The_Middle_Of_Its_Parent() =>
       lift(doc(blockquote(p("<before>one"),p("<a>two"),p("<after>three"))),
           doc(blockquote(p("<before>one")),p("<a>two"),blockquote(p("<after>three"))));

   [Fact] public void Can_Lift_A_Block_From_The_Start_Of_Its_Parent() =>
       lift(doc(blockquote(p("<a>two"),p("<after>three"))),
           doc(p("<a>two"),blockquote(p("<after>three"))));

   [Fact] public void Can_Lift_A_Block_From_The_End_Of_Its_Parent() =>
       lift(doc(blockquote(p("<before>one"),p("<a>two"))),
           doc(blockquote(p("<before>one")),p("<a>two")));

   [Fact] public void Can_Lift_A_Single_Child() =>
       lift(doc(blockquote(p("<a>t<in>wo"))),
           doc(p("<a>t<in>wo")));

   [Fact] public void Can_Lift_Multiple_Blocks() =>
       lift(doc(blockquote(blockquote(p("on<a>e"),p("tw<b>o")),p("three"))),
           doc(blockquote(p("on<a>e"),p("tw<b>o"),p("three"))));

   [Fact] public void Finds_A_Valid_Range_From_A_Lopsided_Selection() =>
       lift(doc(p("start"),blockquote(blockquote(p("a"),p("<a>b")),p("<b>c"))),
           doc(p("start"),blockquote(p("a"),p("<a>b")),p("<b>c")));

   [Fact] public void Can_Lift_From_A_Nested_Node() =>
       lift(doc(blockquote(blockquote(p("<1>one"),p("<a>two"),p("<3>three"),p("<b>four"),p("<5>five")))),
           doc(blockquote(blockquote(p("<1>one")),p("<a>two"),p("<3>three"),p("<b>four"),blockquote(p("<5>five")))));

   [Fact] public void Can_Lift_From_A_List() =>
       lift(doc(ul(li(p("one")),li(p("two<a>")),li(p("three")))),
           doc(ul(li(p("one"))),p("two<a>"),ul(li(p("three")))));

   [Fact] public void Can_Lift_From_The_End_Of_A_List() =>
       lift(doc(ul(li(p("a")),li(p("b<a>")),"<1>")),
           doc(ul(li(p("a"))),p("b<a>"),"<1>"));

    private static void wrap(Node doc, Node expect, string type, Attrs? attrs = null) {
        var range = doc.Resolve(tag(doc, "a")).BlockRange(doc.Resolve(tag(doc,"b","a")));
        Util.testTransform(new Transform(doc).Wrap(range!, Structure.FindWrapping(range!, schema.Nodes[type], attrs)!), expect);
    }

   [Fact] public void Can_Wrap_In_A_Blockquote() =>
       wrap(doc(p("one"),p("<a>two"),p("three")),
           doc(p("one"),blockquote(p("<a>two")),p("three")),
            "blockquote");

   [Fact] public void Can_Wrap_Two_Paragraphs() =>
       wrap(doc(p("one<1>"),p("<a>two"),p("<b>three"),p("four<4>")),
           doc(p("one<1>"),blockquote(p("<a>two"),p("three")),p("four<4>")),
            "blockquote");

   [Fact] public void Can_Wrap_In_A_List() =>
       wrap(doc(p("<a>one"),p("<b>two")),
           doc(ol(li(p("<a>one"),p("<b>two")))),
            "ordered_list");

   [Fact] public void Can_Wrap_In_A_Nested_List() =>
       wrap(doc(ol(li(p("<1>one")),li(p("..."),p("<a>two"),p("<b>three")),li(p("<4>four")))),
           doc(ol(li(p("<1>one")),li(p("..."),ol(li(p("<a>two"),p("<b>three")))),li(p("<4>four")))),
            "ordered_list");

   [Fact] public void Includes_Half_Covered_Parent_Nodes() =>
       wrap(doc(blockquote(p("<1>one"),p("two<a>")),p("three<b>")),
           doc(blockquote(blockquote(p("<1>one"),p("two<a>")),p("three<b>"))),
            "blockquote");

    private static void type(Node doc, Node expect, string nodeType, Attrs? attrs = null) {
        Util.testTransform(new Transform(doc).SetBlockType(tag(doc, "a"), tag(doc, "b", "a"), schema.Nodes[nodeType], attrs),
                           expect);
    }

   [Fact] public void Can_Change_A_Single_Textblock() =>
       type(doc(p("am<a>_I")),
           doc(h2("am_I")),
            "heading", new(){["level"] = 2});

   [Fact] public void Can_Change_Multiple_Blocks() =>
       type(doc(h1("<a>hello"),p("there"),p("<b>you"),p("end")),
           doc(pre("hello"),pre("there"),pre("you"),p("end")),
            "code_block");

   [Fact] public void Can_Change_A_Wrapped_Block() =>
       type(doc(blockquote(p("one<a>"),p("two<b>"))),
           doc(blockquote(h1("one<a>"),h1("two<b>"))),
            "heading",new(){["level"] = 1});

   [Fact] public void Clears_Markup_When_Necessary() =>
       type(doc(p("hello<a> ",em("world"))),
           doc(pre("hello world")),
            "code_block");

    [Fact] public void Removes_non_allowed_nodes() =>
      type(doc(p("<a>one", img(), "two", img(), "three")),
           doc(pre("onetwothree")),
           "code_block");

    [Fact] public void Removes_newlines_in_non_code() =>
      type(doc(pre("<a>one\ntwo\nthree")),
           doc(p("one two three")),
           "paragraph");

   [Fact] public void Only_Clears_Markup_When_Needed() =>
       type(doc(p("hello<a> ",em("world"))),
           doc(h1("hello<a> ",em("world"))),
            "heading",new(){["level"] = 1});

   [Fact] public void Works_After_Another_Step() {
     var d = doc(p("f<x>oob<y>ar"),p("baz<a>"));
     var tr = new Transform(d).Delete(d.Tag()["x"],d.Tag()["y"]);
     var pos = tr.Mapping.Map(d.Tag()["a"]);
     tr.SetBlockType(pos,pos,schema.Nodes["heading"],new(){["level"] = 1});
     Util.testTransform(tr,doc(p("f<x><y>ar"),h1("baz<a>")));
    }

   [Fact] public void Skips_Nodes_That_Cant_Be_Changed_Due_To_Constraints() =>
       type(doc(p("<a>hello",img()),p("okay"),ul(li(p("foo<b>")))),
           doc(pre("<a>hello"),pre("okay"),ul(li(p("foo<b>")))),
            "code_block");

    private static void markup(Node doc, Node expect, string type, Attrs? attrs = null) {
        Util.testTransform(new Transform(doc).SetNodeMarkup(tag(doc, "a"), schema.Nodes[type], attrs), expect);
    }

   [Fact] public void Can_Change_A_Textblock() =>
      markup(doc("<a>",p("foo")),
             doc(h1("foo")),
              "heading",new() {["level"] = 1});

   [Fact] public void Can_Change_An_Inline_Node() =>
      markup(doc(p("foo<a>",img(),"bar")),
             doc(p("foo",img(new {src = "bar",alt = "y"}),"bar")),
              "image",new() {["src"] = "bar",["alt"] = "y"});


    private static void repl(Node doc, object? source, Node expect) {
        var slice = source switch {
            null => Slice.Empty,
            Slice s => s,
            Node n => n.Slice(n.Tag()["a"], n.Tag()["b"]),
            _ => throw new ArgumentException("Invalid source")
        };
        Util.testTransform(new Transform(doc).Replace(tag(doc, "a"), tag(doc, "b", "a"), slice), expect);
    }

   [Fact] public void Can_Delete_Text() =>
      repl(doc(p("hell<a>o_Y<b>ou")),
           null,
           doc(p("hell<a><b>ou")));

   [Fact] public void Repl_Can_Join_Blocks() =>
      repl(doc(p("hell<a>o"),p("y<b>ou")),
           null,
           doc(p("hell<a><b>ou")));

   [Fact] public void Can_Delete_Right_Leaning_Lopsided_Regions() =>
      repl(doc(blockquote(p("ab<a>c")),"<b>",p("def")),
           null,
           doc(blockquote(p("ab<a>")),"<b>",p("def")));

   [Fact] public void Can_Delete_Left_Leaning_Lopsided_Regions() =>
      repl(doc(p("abc"),"<a>",blockquote(p("d<b>ef"))),
           null,
           doc(p("abc"),"<a>",blockquote(p("<b>ef"))));

   [Fact] public void Can_Overwrite_Text() =>
      repl(doc(p("hell<a>o_Y<b>ou")),
           doc(p("<a>i_K<b>")),
           doc(p("hell<a>i_K<b>ou")));

   [Fact] public void Can_Insert_Text() =>
      repl(doc(p("hell<a><b>o")),
           doc(p("<a>i_K<b>")),
           doc(p("helli_K<a><b>o")));

   [Fact] public void Can_Add_A_Textblock() =>
      repl(doc(p("hello<a>you")),
           doc("<a>",p("there"),"<b>"),
           doc(p("hello"),p("there"),p("<a>you")));

   [Fact] public void Can_Insert_While_Joining_Textblocks() =>
      repl(doc(h1("he<a>llo"),p("arg<b>!")),
           doc(p("1<a>2<b>3")),
           doc(h1("he2!")));

   [Fact] public void Will_Match_Open_List_Items() =>
      repl(doc(ol(li(p("one<a>")),li(p("three")))),
           doc(ol(li(p("<a>half")),li(p("two")),"<b>")),
           doc(ol(li(p("onehalf")),li(p("two")),li(p("three")))));

   [Fact] public void Merges_Blocks_Across_Deleted_Content() =>
      repl(doc(p("a<a>"),p("b"),p("<b>c")),
           null,
           doc(p("a<a><b>c")));

   [Fact] public void Can_Merge_Text_Down_From_Nested_Nodes() =>
      repl(doc(h1("wo<a>ah"),blockquote(p("ah<b>ha"))),
           null,
           doc(h1("wo<a><b>ha")));

   [Fact] public void Can_Merge_Text_Up_Into_Nested_Nodes() =>
      repl(doc(blockquote(p("foo<a>bar")),p("middle"),h1("quux<b>baz")),
           null,
           doc(blockquote(p("foo<a><b>baz"))));

   [Fact] public void Will_Join_Multiple_Levels_When_Possible() =>
      repl(doc(blockquote(ul(li(p("a")),li(p("b<a>")),li(p("c")),li(p("<b>d")),li(p("e"))))),
           null,
           doc(blockquote(ul(li(p("a")),li(p("b<a><b>d")),li(p("e"))))));

   [Fact] public void Can_Replace_A_Piece_Of_Text() =>
      repl(doc(p("he<before>llo<a>_W<after>orld")),
           doc(p("<a>_Big<b>")),
           doc(p("he<before>llo_Big_W<after>orld")));

   [Fact] public void Respects_Open_Empty_Nodes_At_The_Edges() =>
      repl(doc(p("one<a>two")),
           doc(p("a<a>"),p("hello"),p("<b>b")),
           doc(p("one"),p("hello"),p("<a>two")));

   [Fact] public void Can_Completely_Overwrite_A_Paragraph() =>
      repl(doc(p("one<a>"),p("t<inside>wo"),p("<b>three<end>")),
           doc(p("a<a>"),p("TWO"),p("<b>b")),
           doc(p("one<a>"),p("TWO"),p("<inside>three<end>")));

   [Fact] public void Joins_Marks() =>
      repl(doc(p("foo ",em("bar<a>baz"),"<b>_Quux")),
           doc(p("foo ",em("xy<a>zzy"),"_Foo<b>")),
           doc(p("foo ",em("barzzy"),"_Foo_Quux")));

   [Fact] public void Can_Replace_Text_With_A_Break() =>
      repl(doc(p("foo<a>b<inside>b<b>bar")),
           doc(p("<a>",br(),"<b>")),
           doc(p("foo",br(),"<inside>bar")));

   [Fact] public void Can_Join_Different_Blocks() =>
      repl(doc(h1("hell<a>o"),p("by<b>e")),
           null,
           doc(h1("helle")));

   [Fact] public void Can_Restore_A_List_Parent() =>
      repl(doc(h1("hell<a>o"),"<b>"),
           doc(ol(li(p("on<a>e")),li(p("tw<b>o")))),
           doc(h1("helle"),ol(li(p("tw")))));

   [Fact] public void Can_Restore_A_List_Parent_And_Join_Text_After_It() =>
      repl(doc(h1("hell<a>o"),p("yo<b>u")),
           doc(ol(li(p("on<a>e")),li(p("tw<b>o")))),
           doc(h1("helle"),ol(li(p("twu")))));

   [Fact] public void Can_Insert_Into_An_Empty_Block() =>
      repl(doc(p("a"),p("<a>"),p("b")),
           doc(p("x<a>y<b>z")),
           doc(p("a"),p("y<a>"),p("b")));

   [Fact] public void Doesnt_Change_The_Nesting_Of_Blocks_After_The_Selection() =>
      repl(doc(p("one<a>"),p("two"),p("three")),
           doc(p("outside<a>"),blockquote(p("inside<b>"))),
           doc(p("one"),blockquote(p("inside")),p("two"),p("three")));

   [Fact] public void Can_Close_A_Parent_Node() =>
      repl(doc(blockquote(p("b<a>c"),p("d<b>e"),p("f"))),
           doc(blockquote(p("x<a>y")),p("after"),"<b>"),
           doc(blockquote(p("b<a>y")),p("after"),blockquote(p("<b>e"),p("f"))));

   [Fact] public void Accepts_Lopsided_Regions() =>
      repl(doc(blockquote(p("b<a>c"),p("d<b>e"),p("f"))),
           doc(blockquote(p("x<a>y")),p("z<b>")),
           doc(blockquote(p("b<a>y")),p("z<b>e"),blockquote(p("f"))));

   [Fact] public void Can_Close_Nested_Parent_Nodes() =>
      repl(doc(blockquote(blockquote(p("one"),p("tw<a>o"),p("t<b>hree<3>"),p("four<4>")))),
           doc(ol(li(p("hello<a>world")),li(p("bye"))),p("ne<b>xt")),
           doc(blockquote(blockquote(p("one"),p("tw<a>world"),ol(li(p("bye"))),p("ne<b>hree<3>"),p("four<4>")))));

   [Fact] public void Will_Close_Open_Nodes_To_The_Right() =>
      repl(doc(p("x"),"<a>"),
           doc("<a>",ul(li(p("a")),li("<b>",p("b")))),
           doc(p("x"),ul(li(p("a")),li(p())),"<a>"));

   [Fact] public void Can_Delete_The_Whole_Document() =>
      repl(doc("<a>",h1("hi"),p("you"),"<b>"),
           null,
           doc(p()));

   [Fact] public void Preserves_An_Empty_Parent_To_The_Left() =>
      repl(doc(blockquote("<a>",p("hi")),p("b<b>x")),
           doc(p("<a>hi<b>")),
           doc(blockquote(p("hix"))));

   [Fact] public void Drops_An_Empty_Parent_To_The_Right() =>
      repl(doc(p("x<a>hi"),blockquote(p("yy"),"<b>"),p("c")),
           doc(p("<a>hi<b>")),
           doc(p("xhi"),p("c")));

   [Fact] public void Drops_An_Empty_Node_At_The_Start_Of_The_Slice() =>
      repl(doc(p("<a>x")),
           doc(blockquote(p("hi"),"<a>"),p("b<b>")),
           doc(p(),p("bx")));

   [Fact] public void Drops_An_Empty_Node_At_The_End_Of_The_Slice() =>
      repl(doc(p("<a>x")),
           doc(p("b<a>"),blockquote("<b>",p("hi"))),
           doc(p(),blockquote(p()),p("x")));

   [Fact] public void Does_Nothing_When_Given_An_Unfittable_Slice() =>
      repl(p("<a>x"),
           new Slice(Fragment.From(new List<Node>{blockquote(),hr()}),0,0),
           p("x"));

   [Fact] public void Doesnt_Drop_Content_When_Things_Only_Fit_At_The_Top_Level() =>
      repl(doc(p("foo"),"<a>",p("bar<b>")),
           ol(li(p("<a>a")),li(p("b<b>"))),
           doc(p("foo"),p("a"),ol(li(p("b")))));

   [Fact] public void Preserves_OpenEnd_When_Top_Isnt_Placed() =>
      repl(doc(ul(li(p("ab<a>cd")),li(p("ef<b>gh")))),
           doc(ul(li(p("ABCD")),li(p("EFGH")))).Slice(5,13,true),
           doc(ul(li(p("abCD")),li(p("EFgh")))));

   [Fact] public void Will_Auto_Close_A_List_Item_When_It_Fits_In_A_List() =>
      repl(doc(ul(li(p("foo")),"<a>",li(p("bar")))),
           ul(li(p("a<a>bc")),li(p("de<b>f"))),
           doc(ul(li(p("foo")),li(p("bc")),li(p("de")),li(p("bar")))));

   [Fact] public void Finds_The_Proper_OpenEnd_Value_When_Unwrapping_A_Deep_Slice() =>
      repl(doc("<a>",p(),"<b>"),
           doc(blockquote(blockquote(blockquote(p("hi"))))).Slice(3,6,true),
           doc(p("hi")));

    private static Schema ms = new Schema(new() {
        Nodes = new(schema.Spec.Nodes) {["doc"] = new() {Content = "block+", Marks = "_"}},
        Marks = new(schema.Spec.Marks)
    });

   [Fact] public void Preserves_Marks_On_Block_Nodes() {
     var tr = new Transform(ms.Node("doc",null,new NodeList {
       ms.Node("paragraph",null, ms.Text("hey"),new() {ms.Mark("em")}),
       ms.Node("paragraph",null, ms.Text("ok"),new() {ms.Mark("strong")})
     }));
     tr.Replace(2,7,tr.Doc.Slice(2,7));
     ist(tr.Doc,tr.Before,eq);
    }

   [Fact] public void Preserves_Marks_On_Open_Slice_Block_Nodes() {
     var tr = new Transform(ms.Node("doc",null,ms.Node("paragraph",null,ms.Text("a"))));
     tr.Replace(3,3,ms.Node("doc",null,
       ms.Node("paragraph",null,ms.Text("b"), new() {ms.Mark("em")})
     ).Slice(1,3));
     ist(tr.Doc.ChildCount,2);
     ist(tr.Doc.LastChild!.Marks.Count,1);
    }

    //_A_Schema_That_Enforces_A_Heading_And_A_Body_At_The_Top_Level
   private static Schema hbSchema = new Schema(new() {
     Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes)
        .Append("doc", new() { Content = "heading body" })
        .Append("body", new() { Content = "block+" })
    });

    private static dynamic hb = BuildersDynamic(hbSchema, new() {
        ["p"] = new {nodeType = "paragraph"},
        ["b"] = new {nodeType = "body"},
        ["h"] = new {nodeType = "heading",level = 1},
    });

   [Fact] public void Can_Unwrap_A_Paragraph_When_Replacing_Into_A_Strict_Schema() {
      var tr = new Transform(hb.doc(hb.h("Head"), hb.b(hb.p("Content"))));
      tr.Replace(0, tr.Doc.Content.Size, tr.Doc.Slice(7, 16));
      ist(tr.Doc, (Node)hb.doc(hb.h("Content"), hb.b(hb.p())), eq);
    }

   [Fact] public void Can_Unwrap_A_Body_After_A_Placed_Node() {
     var tr = new Transform(hb.doc(hb.h("Head"),hb.b(hb.p("Content"))));
     tr.Replace(7,7,tr.Doc.Slice(0,tr.Doc.Content.Size));
     ist(tr.Doc,(Node)hb.doc(hb.h("Head"),hb.b(hb.h("Head"),hb.p("Content"),hb.p("Content"))), eq);
    }

   [Fact] public void Can_Wrap_A_Paragraph_In_A_Body_Even_When_Its_Not_The_First_Node() {
     var tr = new Transform(hb.doc(hb.h("Head"),hb.b(hb.p("One"),hb.p("Two"))));
     tr.Replace(0,tr.Doc.Content.Size,tr.Doc.Slice(8,16));
     ist(tr.Doc,(Node)hb.doc(hb.h("One"),hb.b(hb.p("Two"))),eq);
    }

   [Fact] public void Can_Split_A_Fragment_And_Place_Its_Children_In_Different_Parents() {
     var tr = new Transform(hb.doc(hb.h("Head"),hb.b(hb.h("One"),hb.p("Two"))));
     tr.Replace(0,tr.Doc.Content.Size,tr.Doc.Slice(7,17));
     ist(tr.Doc,(Node)hb.doc(hb.h("One"),hb.b(hb.p("Two"))),eq);
    }

   [Fact] public void Will_Insert_Filler_Nodes_Before_A_Node_When_Necessary() {
     var tr = new Transform(hb.doc(hb.h("Head"),hb.b(hb.p("One"))));
     tr.Replace(0,tr.Doc.Content.Size,tr.Doc.Slice(6,tr.Doc.Content.Size));
     ist(tr.Doc,(Node)hb.doc(hb.h(),hb.b(hb.p("One"))),eq);
    }

   [Fact] public void Doesnt_Fail_When_Moving_Text_Would_Solve_An_Unsatisfied_Content_Constraint() {
     var s = new Schema(new() {
        Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes)
            .Append("title", new() { Content = "text*"})
            .Append("doc", new() { Content = "title block*"})
     });
     var tr = new Transform(s.Node("doc",null,s.Node("title",null,s.Text("hi"))));
     tr.Replace(1,1,s.Node("bullet_list",null,new NodeList {
       s.Node("list_item",null,s.Node("paragraph",null,s.Text("one"))),
       s.Node("list_item",null,s.Node("paragraph",null,s.Text("two")))
     }).Slice(2,12));
     ist(tr.Steps.Count > 0);
    }

   [Fact] public void Doesnt_Fail_When_Pasting_A_Half_Open_Slice_With_A_Title_And_A_Code_Block_Into_An_Empty_Title() {
     var s = new Schema(new() {
       Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes)
          .Append("title", new() { Content = "text*"})
          .Append("doc", new() { Content = "title? block*"})
      });
     var tr = new Transform(s.Node("doc",null, s.Node("title",null)));
     tr.Replace(1,1,s.Node("doc",null, new NodeList {
       s.Node("title",null,s.Text("title")),
       s.Node("code_block",null,s.Text("two")),
     }).Slice(1));
     ist(tr.Steps.Count > 0);
    }

   [Fact] public void Doesnt_Fail_When_Pasting_A_Half_Open_Slice_With_A_Heading_And_A_Code_Block_Into_An_Empty_Title() {
     var s = new Schema(new() {
       Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes)
         .Append("title", new() {Content = "text*"})
         .Append("doc", new() {Content = "title? block*"})
     });
     var tr = new Transform(s.Node("doc",null,s.Node("title")));
     tr.Replace(1,1,s.Node("doc",null, new NodeList {
       s.Node("heading", new() {["level"] = 1}, s.Text("heading")),
       s.Node("code_block",null,s.Text("code")),
     }).Slice(1));
     ist(tr.Steps.Count > 0);
    }

   [Fact] public void Can_Handle_Replacing_In_Nodes_With_Fixed_Content() {
     var s = new Schema(new() {
       Nodes =  new() {
         ["doc"] = new() {Content = "block+"},
         ["a"] = new() {Content = "inline*"},
         ["b"] = new() {Content = "inline*"},
         ["block"] = new() {Content = "a b"},
         ["text"] = new() {Group = "inline"}
        }
      });

     var doc =s.Node("doc",null,
       s.Node("block",null,new NodeList {s.Node("a",null,s.Text("aa")),s.Node("b",null,s.Text("bb"))}));
     int from = 3, to = doc.Content.Size;
     ist(new Transform(doc).Replace(from,to,doc.Slice(from,to)).Doc,doc,eq);
    }

   [Fact] public void Keeps_Isolating_Nodes_Together() {
     var s = new Schema(new() {
       Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes)
         .Append("iso", new() {
           Group = "block",
           Content = "block+",
           Isolating = true
          })
      });
     var doc =s.Node("doc",null,s.Node("paragraph",null,s.Text("one")));
     var iso = Fragment.From(s.Node("iso",null,s.Node("paragraph",null,s.Text("two"))));
     ist(new Transform(doc).Replace(2,3, new Slice(iso,2,0)).Doc,
         s.Node("doc",null, new NodeList {
           s.Node("paragraph",null,s.Text("o")),
           s.Node("iso",null,s.Node("paragraph",null,s.Text("two"))),
           s.Node("paragraph",null,s.Text("e"))
          }),eq);
     ist(new Transform(doc).Replace(2,3,new Slice(iso,2,2)).Doc,
         s.Node("doc",null,s.Node("paragraph",null,s.Text("otwoe"))),eq);
    }

    private static void replRange(Node doc, object source, Node expect) {
        var slice = source switch {
            null => Slice.Empty,
            Slice s => s,
            Node n => n.Slice(n.Tag()["a"], n.Tag()["b"], true),
            _ => throw new ArgumentException("Invalid source")
        };
        Util.testTransform(new Transform(doc).ReplaceRange(tag(doc, "a"), tag(doc, "b", "a"), slice), expect);
    }

   [Fact] public void Replaces_Inline_Content() =>
      replRange(doc(p("foo<a>b<b>ar")),p("<a>xx<b>"),doc(p("foo<a>xx<b>ar")));

   [Fact] public void Replaces_An_Empty_Paragraph_With_A_Heading() =>
      replRange(doc(p("<a>")),doc(h1("<a>text<b>")),doc(h1("text")));

   [Fact] public void Replaces_A_Fully_Selected_Paragraph_With_A_Heading() =>
      replRange(doc(p("<a>abc<b>")),doc(h1("<a>text<b>")),doc(h1("text")));

   [Fact] public void Recreates_A_List_When_Overwriting_A_Paragraph() =>
      replRange(doc(p("<a>")),doc(ul(li(p("<a>foobar<b>")))),doc(ul(li(p("foobar")))));

   [Fact] public void Drops_Context_When_It_Doesnt_Fit() =>
      replRange(doc(ul(li(p("<a>")),li(p("b")))),doc(h1("<a>h<b>")),doc(ul(li(p("h<a>")),li(p("b")))));

   [Fact] public void Can_Replace_A_Node_When_Endpoints_Are_In_Different_Children() =>
      replRange(doc(p("a"),ul(li(p("<a>b")),li(p("c"),blockquote(p("d<b>")))),p("e")),
           doc(h1("<a>x<b>")),
           doc(p("a"),h1("x"),p("e")));

   [Fact] public void Keeps_Defining_Context_When_Inserting_At_The_Start_Of_A_Textblock() =>
      replRange(doc(p("<a>foo")),
           doc(ul(li(p("<a>one")),li(p("two<b>")))),
           doc(ul(li(p("one")),li(p("twofoo")))));

    [Fact]public void Keeps_Defining_Context_When_It_Doesnt_Matches_The_Parent_Markup() {
        var spec = new NodeSpec() {
            Content = "block+",
            Group = "block",
            DefiningForContent = true,
            DefiningAsContext = false,
            Attrs = new() {
            ["color"] = new() {
                Default = (JsonNode)"black"!,
            },
            },
        };
        var s = new Schema(new() {
            Nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes) { ["blockquote"] = spec},
            Marks = schema.Spec.Marks,
        });
        
        var b = BuildersDynamic(s, new() {
            ["b1"] = new { nodeType = "blockquote", color = "#100" },
            ["b2"] = new { nodeType = "blockquote", color = "#200" },
            ["b3"] = new { nodeType = "blockquote", color = "#300" },
            ["b4"] = new { nodeType = "blockquote", color = "#400" },
            ["b5"] = new { nodeType = "blockquote", color = "#500" },
            ["b6"] = new { nodeType = "blockquote", color = "#600" },
            ["p"] = new { nodeType = "paragraph" },
            ["doc"] = new { nodeType = "doc" },
        });

        var b1 = b.b1;
        var b2 = b.b2;
        var b3 = b.b3;
        var b4 = b.b4;
        var b5 = b.b5;
        var b6 = b.b6;
        var p = b.p;
        var doc = b.doc;

        Node source = doc(b.b1(p("<a>b1")), b.b2(p("b2<b>")));

        var before1 = doc(b3(p("b3")), b4(p("<a>")));
        var before2 = doc(b5(p("b5")),b3(p("b3")), b4(p("<a>")));
        var before3 = doc(b6(p("b6")), b5(p("b5")),b3(p("b3")), b4(p("<a>")));

        var expect1 = doc(b3(p("b3")), b1(p("b1")), b2(p("b2")));
        var expect2 = doc(b5(p("b5")), b3(p("b3")), b1(p("b1")), b2(p("b2")));
        var expect3 = doc(b6(p("b6")), b5(p("b5")), b3(p("b3")), b1(p("b1")), b2(p("b2")));

        replRange(before1, source, expect1);
        replRange(before2, source, expect2);
        replRange(before3, source, expect3);
    }

   [Fact] public void Drops_Defining_Context_When_It_Matches_The_Parent_Structure() =>
      replRange(doc(blockquote(p("<a>"))),
           doc(blockquote(p("<a>one<b>"))),
           doc(blockquote(p("one"))));

    [Fact] public void Drops_Defining_Context_When_It_Matches_The_Parent_Structure_In_A_Nested_Context() =>
       repl(doc(ul(li(p("list1"), blockquote(p("<a>"))))),
            doc(blockquote(p("<a>one<b>"))),
            doc(ul(li(p("list1"), blockquote(p("one"))))));

    [Fact] public void Drops_Defining_Context_When_It_Matches_The_Parent_Structure_In_A_Deep_Nested_Context() =>
      repl(doc(ul(li(p("list1"), ul(li(p("list2"), blockquote(p("<a>"))))))),
           doc(blockquote(p("<a>one<b>"))),
           doc(ul(li(p("list1"), ul(li(p("list2"), blockquote(p("one"))))))));

   [Fact] public void Closes_Open_Nodes_At_The_Start() =>
      replRange(doc("<a>",p("abc"),"<b>"),
           doc(ul(li("<a>")),p("def"),"<b>"),
           doc(ul(li(p())),p("def")));

    private static void replRangeWith(Node doc, Node node, Node expect) {
      Util.testTransform(new Transform(doc).ReplaceRangeWith(tag(doc, "a"), tag(doc, "b", "a"), node), expect);
    }

   [Fact] public void Can_Insert_An_Inline_Node() =>
      replRangeWith(doc(p("fo<a>o")),img(),doc(p("fo",img(),"<a>o")));

   [Fact] public void Can_Replace_Content_With_An_Inline_Node() =>
      replRangeWith(doc(p("<a>fo<b>o")),img(),doc(p("<a>",img(),"o")));

   [Fact] public void Can_Replace_A_Block_Node_With_An_Inline_Node() =>
      replRangeWith(doc("<a>",blockquote(p("a")),"<b>"),img(),doc(p(img())));

   [Fact] public void Can_Replace_A_Block_Node_With_A_Block_Node() =>
      replRangeWith(doc("<a>",blockquote(p("a")),"<b>"),hr(),doc(hr()));

   [Fact] public void Can_Insert_A_Block_Quote_In_The_Middle_Of_Text() =>
      replRangeWith(doc(p("foo<a>bar")),hr(),doc(p("foo"),hr(),p("bar")));

   [Fact] public void Can_Replace_Empty_Parents_With_A_Block_Node() =>
      replRangeWith(doc(blockquote(p("<a>"))),hr(),doc(blockquote(hr())));

   [Fact] public void Can_Move_An_Inserted_Block_Forward_Out_Of_Parent_Nodes() =>
      replRangeWith(doc(h1("foo<a>")),hr(),doc(h1("foo"),hr()));

   [Fact] public void Can_Move_An_Inserted_Block_Backward_Out_Of_Parent_Nodes() =>
      replRangeWith(doc(p("a"),blockquote(p("<a>b"))),hr(),doc(p("a"),blockquote(hr(),p("b"))));

    private static void delRange(Node doc, Node expect) {
      Util.testTransform(new Transform(doc).DeleteRange(tag(doc, "a"), tag(doc, "b", "a")), expect);
    }

   [Fact] public void Deletes_The_Given_Range() =>
       delRange(doc(p("fo<a>o"),p("b<b>ar")),doc(p("fo<a><b>ar")));

   [Fact] public void Deletes_Empty_Parent_Nodes() =>
       delRange(doc(blockquote(ul(li("<a>",p("foo"),"<b>")),p("x"))),
          doc(blockquote("<a><b>",p("x"))));

   [Fact] public void Doesnt_Delete_Parent_Nodes_That_Can_Be_Empty() =>
       delRange(doc(p("<a>foo<b>")),doc(p("<a><b>")));

   [Fact] public void Is_Okay_With_Deleting_Empty_Ranges() =>
       delRange(doc(p("<a><b>")),doc(p("<a><b>")));

   [Fact] public void Will_Delete_A_Whole_Covered_Node_Even_If_Selection_Ends_Are_In_Different_Nodes() =>
       delRange(doc(ul(li(p("<a>foo")),li(p("bar<b>"))),p("hi")),doc(p("hi")));

   [Fact] public void Leaves_Wrapping_Textblock_When_Deleting_All_Text_In_It() =>
       delRange(doc(p("a"),p("<a>b<b>")),doc(p("a"),p()));

   [Fact] public void Expands_To_Cover_The_Whole_Parent_Node() =>
       delRange(doc(p("a"),blockquote(blockquote(p("<a>foo")),p("bar<b>")),p("b")),
          doc(p("a"),p("b")));

   [Fact] public void Expands_To_Cover_The_Whole_Document() =>
       delRange(doc(h1("<a>foo"),p("bar"),blockquote(p("baz<b>"))),
          doc(p()));

   [Fact] public void Doesnt_Expand_Beyond_Same_Depth_Textblocks() =>
       delRange(doc(h1("<a>foo"),p("bar"),p("baz<b>")),
          doc(h1()));

   [Fact] public void Deletes_The_Open_Token_When_Deleting_From_Start_To_Past_End_Of_Block() =>
       delRange(doc(h1("<a>foo"),p("b<b>ar")),
          doc(p("ar")));

   [Fact] public void Doesnt_Delete_The_Open_Token_When_The_Range_End_Is_At_End_Of_Its_Own_Block() =>
       delRange(doc(p("one"),h1("<a>two"),blockquote(p("three<b>")),p("four")),
          doc(p("one"),h1(),p("four")));

    private static void addNodeMark(Node doc, Model.Mark mark, Node expect) =>
        Util.testTransform(new Transform(doc).AddNodeMark(tag(doc, "a"), mark), expect);

   [Fact] public void Adds_A_Mark() =>
      addNodeMark(doc(p("<a>",img())),schema.Mark("em"),doc(p("<a>",em(img()))));

   [Fact] public void Doesnt_Duplicate_A_Mark() =>
      addNodeMark(doc(p("<a>",em(img()))),schema.Mark("em"),doc(p("<a>",em(img()))));

   [Fact] public void Replaces_A_Mark() =>
      addNodeMark(doc(p("<a>",a(img()))),schema.Mark("link",new() {["href"] = "x"}),doc(p("<a>",a(new {href = "x"},img()))));

    private static void rmNodeMark(Node doc, OneOf<Model.Mark, MarkType> mark, Node expect) =>
        Util.testTransform(new Transform(doc).RemoveNodeMark(tag(doc, "a"), mark), expect);

   [Fact] public void Removes_A_Mark() =>
     rmNodeMark(doc(p("<a>",em(img()))),schema.Mark("em"),doc(p("<a>",img())));

   [Fact] public void Doesnt_Do_Anything_When_There_Is_No_Mark() =>
     rmNodeMark(doc(p("<a>",img())),schema.Mark("em"),doc(p("<a>",img())));

   [Fact] public void Can_Remove_A_Mark_From_Multiple_Marks() =>
     rmNodeMark(doc(p("<a>",em(a(img())))),schema.Mark("em"),doc(p("<a>",a(img()))));

    private static void set(Node doc, string attr, JsonNode value, Node expect) =>
        Util.testTransform(new Transform(doc).SetNodeAttribute(tag(doc, "a"), attr, value), expect);

   [Fact] public void Sets_An_Attribute() =>
     set(doc("<a>",h1("a")),"level",2,doc("<a>",h2("a")));
}

public static class Util {
    public static Transform invert(Transform transform) {
        var @out = new Transform(transform.Doc);
        for (var i = transform.Steps.Count - 1; i >= 0; i--)
            @out.Step(transform.Steps[i].Invert(transform.Docs[i]));
        return @out;
    }

    public static void testMapping(Mapping mapping, int pos, int newPos) {
        var mapped = mapping.Map(pos, 1);
        ist(mapped, newPos);

        var remap = new Mapping(mapping.Maps.Select(m => m.Invert()).ToList());
        var mapFrom = mapping.Maps.Count;
        for (var i = mapping.Maps.Count - 1; i >= 0; i--)
            remap.AppendMap(mapping.Maps[i], --mapFrom);
        ist(remap.Map(pos, 1), pos);
    }

    public static void testStepJSON(Transform tr) {
        // Supporting undefined Json properties in C# requires a lot of extra work
        // so we round-trip the doc and steps to string as well.

        var docJSON = tr.Before.ToJSON();
        var docString = docJSON.ToJson();
        var doc = Node.FromJSON(tr.Doc.Type.Schema, NodeDto.FromJson(docString)!);
        var newTR = new Transform(doc);

        tr.Steps.ForEach(step => newTR.Step(Step.FromJSON(tr.Doc.Type.Schema, StepDto.FromJson(step.ToJSON().ToJson())!)));
        ist(tr.Doc, newTR.Doc, eq);
    }

    public static void testTransform(Transform tr, Node expect) {
        // outputTransform(tr, expect);
        ist(tr.Doc, expect, eq);
        ist(invert(tr).Doc, tr.Before, eq);

        testStepJSON(tr);

        foreach (var (tag, _) in expect.Tag())
            testMapping(tr.Mapping, tr.Before.Tag()[tag], expect.Tag()[tag]);
    }

    private record Output(int schema, NodeDto start, List<Step> steps, Node result, List<(int, int)> mapping) {}

    // public static Output? output { get; } = null;

    // public void outputTransform(Transform tr, Node expected) {
    //     if (output && tr.Steps.Count > 0) {

    //     }
    // }
}