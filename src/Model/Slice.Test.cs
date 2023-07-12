using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.TestBuilder;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class SliceTest {
    public ITestOutputHelper Out;

    public record ResolveResult(Node node, int start, int end);
    public static Node testDoc { get; } = doc(p("ab"), blockquote(p(em("cd"), "ef")));
    public static ResolveResult _doc { get; } = new(testDoc, 0, 12);
    public static ResolveResult _p1 { get; } = new(testDoc.Child(0), 1, 3);
    public static ResolveResult _blk { get; } = new(testDoc.Child(1), 5, 11);
    public static ResolveResult _p2 { get; } = new(_blk.node.Child(0), 6, 10);

    public SliceTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static void t(Node doc, Node expect, int openStart, int openEnd) {

        int? tagB = doc.Tag().ContainsKey("b") ? doc.Tag()["b"] : null;

        var slice = doc.Slice(doc.Tag().GetValueOrDefault("a"), tagB);
        slice.Content.Eq(expect.Content).Should().BeTrue();
        slice.OpenStart.Should().Be(openStart);
        slice.OpenEnd.Should().Be(openEnd);
    }

   [Fact] public void Can_Cut_Half_A_Paragraph() =>
      t(doc(p("hello<b> world")),doc(p("hello")), 0, 1);

   [Fact] public void Can_Cut_To_The_End_Of_A_Pragraph() =>
      t(doc(p("hello<b>")),doc(p("hello")),0,1);

   [Fact] public void Leaves_Off_Extra_Content() =>
      t(doc(p("hello<b>_World"),p("rest")),doc(p("hello")),0,1);

   [Fact] public void Preserves_Styles() =>
      t(doc(p("hello ",em("WOR<b>LD"))),doc(p("hello ",em("WOR"))),0,1);

   [Fact] public void Can_Cut_Multiple_Blocks() =>
      t(doc(p("a"),p("b<b>")),doc(p("a"),p("b")),0,1);

   [Fact] public void Can_Cut_To_A_Top_Level_Position() =>
      t(doc(p("a"),"<b>",p("b")),doc(p("a")),0,0);

   [Fact] public void Can_Cut_To_A_Deep_Position() =>
      t(doc(blockquote(ul(li(p("a")),li(p("b<b>"))))),
        doc(blockquote(ul(li(p("a")),li(p("b"))))),0,4);

   [Fact] public void Can_Cut_Everything_After_A_Position() =>
      t(doc(p("hello<a> world")),doc(p(" world")),1,0);

   [Fact] public void Can_Cut_From_The_Start_Of_A_Textblock() =>
      t(doc(p("<a>hello")),doc(p("hello")),1,0);

   [Fact] public void Leaves_Off_Extra_Content_Before() =>
      t(doc(p("foo"),p("bar<a>baz")),doc(p("baz")),1,0);

   [Fact] public void Preserves_Styles_After_Cut() =>
      t(doc(p("a_Sentence_With_An ",em("emphasized ",a("li<a>nk")),"_In_It")),
        doc(p(em(a("nk")),"_In_It")),1,0);

   [Fact] public void Preserves_Styles_Started_After_Cut() =>
      t(doc(p("a ",em("sentence"),"_Wi<a>th ",em("text"),"_In_It")),
        doc(p("th ",em("text"),"_In_It")),1,0);

   [Fact] public void Can_Cut_From_A_Top_Level_Position() =>
      t(doc(p("a"),"<a>",p("b")),doc(p("b")),0,0);

   [Fact] public void Can_Cut_From_A_Deep_Position() =>
      t(doc(blockquote(ul(li(p("a")),li(p("<a>b"))))),
        doc(blockquote(ul(li(p("b"))))),4,0);

   [Fact] public void Can_Cut_Part_Of_A_Text_Node() =>
      t(doc(p("hell<a>o_Wo<b>rld")),p("o_Wo"),0,0);

   [Fact] public void Can_Cut_Across_Paragraphs() =>
      t(doc(p("on<a>e"),p("t<b>wo")),doc(p("e"),p("t")),1,1);

   [Fact] public void Can_Cut_Part_Of_Marked_Text() =>
      t(doc(p("here's noth<a>ing and ",em("here's e<b>m"))),
        p("ing and ",em("here's e")),0,0);

   [Fact] public void Can_Cut_Across_Different_Depths() =>
      t(doc(ul(li(p("hello")),li(p("wo<a>rld")),li(p("x"))),p(em("bo<b>o"))),
        doc(ul(li(p("rld")),li(p("x"))),p(em("bo"))),3,1);

   [Fact] public void Can_Cut_Between_Deeply_Nested_Nodes() =>
      t(doc(blockquote(p("foo<a>bar"),ul(li(p("a")),li(p("b"),"<b>",p("c"))),p("d"))),
        blockquote(p("bar"),ul(li(p("a")),li(p("b")))),1,2);

    [Fact] public void Can_Include_Parents() {
        var d = doc(blockquote(p("fo<a>o"),p("bar<b>")));
        var slice = d.Slice(d.Tag()["a"], d.Tag()["b"], true);
        ist(slice.ToString(), """<blockquote(paragraph("o"), paragraph("bar"))>(2,2)""");
    }
}