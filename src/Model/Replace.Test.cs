using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.TestBuilder;
using System.Text.RegularExpressions;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class ReplaceTest {
    public ITestOutputHelper Out;

    public ReplaceTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static void rpl(Node doc, Node? insert, Node expected) {
        var slice = insert is not null ? insert.Slice(insert.Tag()["a"], insert.Tag()["b"]) : Slice.Empty;
        ist(doc.Replace(doc.Tag()["a"], doc.Tag()["b"], slice), expected, eq);
    }

   [Fact] public void Joins_On_Delete() =>
       rpl(doc(p("on<a>e"),p("t<b>wo")),null,doc(p("onwo")));

   [Fact] public void Merges_Matching_Blocks() =>
       rpl(doc(p("on<a>e"),p("t<b>wo")),doc(p("xx<a>xx"),p("yy<b>yy")),doc(p("onxx"),p("yywo")));

   [Fact] public void Merges_When_Adding_Text() =>
       rpl(doc(p("on<a>e"),p("t<b>wo")),
           doc(p("<a>H<b>")),
           doc(p("onHwo")));

   [Fact] public void Can_Insert_Text() =>
       rpl(doc(p("before"),p("on<a><b>e"),p("after")),
           doc(p("<a>H<b>")),
           doc(p("before"),p("onHe"),p("after")));

   [Fact] public void Doesnt_Merge_Non_Matching_Blocks() =>
       rpl(doc(p("on<a>e"),p("t<b>wo")),
           doc(h1("<a>H<b>")),
           doc(p("onHwo")));

   [Fact] public void Can_Merge_A_Nested_Node() =>
       rpl(doc(blockquote(blockquote(p("on<a>e"),p("t<b>wo")))),
           doc(p("<a>H<b>")),
           doc(blockquote(blockquote(p("onHwo")))));

   [Fact] public void Can_Replace_Within_A_Block() =>
       rpl(doc(blockquote(p("a<a>bc<b>d"))),
           doc(p("x<a>y<b>z")),
           doc(blockquote(p("ayd"))));

   [Fact] public void Can_Insert_A_Lopsided_Slice() =>
       rpl(doc(blockquote(blockquote(p("on<a>e"),p("two"),"<b>",p("three")))),
           doc(blockquote(p("aa<a>aa"),p("bb"),p("cc"),"<b>",p("dd"))),
           doc(blockquote(blockquote(p("onaa"),p("bb"),p("cc"),p("three")))));

   [Fact] public void Can_Insert_A_Deep_Lopsided_Slice() =>
       rpl(doc(blockquote(blockquote(p("on<a>e"),p("two"),p("three")),"<b>",p("x"))),
           doc(blockquote(p("aa<a>aa"),p("bb"),p("cc")),"<b>",p("dd")),
           doc(blockquote(blockquote(p("onaa"),p("bb"),p("cc")),p("x"))));

   [Fact] public void Can_Merge_Multiple_Levels() =>
       rpl(doc(blockquote(blockquote(p("hell<a>o"))),blockquote(blockquote(p("<b>a")))),
           null,
           doc(blockquote(blockquote(p("hella")))));

   [Fact] public void Can_Merge_Multiple_Levels_While_Inserting() =>
       rpl(doc(blockquote(blockquote(p("hell<a>o"))),blockquote(blockquote(p("<b>a")))),
           doc(p("<a>i<b>")),
           doc(blockquote(blockquote(p("hellia")))));

   [Fact] public void Can_Insert_A_Split() =>
       rpl(doc(p("foo<a><b>bar")),
           doc(p("<a>x"),p("y<b>")),
           doc(p("foox"),p("ybar")));

   [Fact] public void Can_Insert_A_Deep_Split() =>
       rpl(doc(blockquote(p("foo<a>x<b>bar"))),
           doc(blockquote(p("<a>x")),blockquote(p("y<b>"))),
           doc(blockquote(p("foox")),blockquote(p("ybar"))));

   [Fact] public void Can_Add_A_Split_One_Level_Up() =>
       rpl(doc(blockquote(p("foo<a>u"),p("v<b>bar"))),
           doc(blockquote(p("<a>x")),blockquote(p("y<b>"))),
           doc(blockquote(p("foox")),blockquote(p("ybar"))));

   [Fact] public void Keeps_The_Node_Type_Of_The_Left_Node() =>
       rpl(doc(h1("foo<a>bar"),"<b>"),
           doc(p("foo<a>baz"),"<b>"),
           doc(h1("foobaz")));

   [Fact] public void Keeps_The_Node_Type_Even_When_Empty() =>
       rpl(doc(h1("<a>bar"),"<b>"),
           doc(p("foo<a>baz"),"<b>"),
           doc(h1("baz")));

    private static void bad(Node doc, Node? insert, string pattern) {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var slice = insert is not null ? insert.Slice(insert.Tag()["a"], insert.Tag()["b"]) : Slice.Empty;
        var act = () =>
            doc.Replace(doc.Tag()["a"], doc.Tag()["b"], slice);
        act.Should().Throw<Exception>().Where(e => regex.IsMatch(e.Message));
    }

   [Fact] public void Doesnt_Allow_The_Left_Side_To_Be_Too_Deep() =>
       bad(doc(p("<a><b>")),
           doc(blockquote(p("<a>")),"<b>"),
           "deeper");

   [Fact] public void Doesnt_Allow_A_Depth_Mismatch() =>
       bad(doc(p("<a><b>")),
           doc("<a>",p("<b>")),
           "inconsistent");

   [Fact] public void Rejects_A_Bad_Fit() =>
       bad(doc("<a><b>"),
           doc(p("<a>foo<b>")),
           "invalid content");

   [Fact] public void Rejects_Unjoinable_Content() =>
       bad(doc(ul(li(p("a")),"<a>"),"<b>"),
           doc(p("foo","<a>"),"<b>"),
           "cannot join");

   [Fact] public void Rejects_An_Unjoinable_Delete() =>
       bad(doc(blockquote(p("a"),"<a>"),ul("<b>",li(p("b")))),
           null,
           "cannot join");

   [Fact] public void Check_Content_Validity() =>
       bad(doc(blockquote("<a>",p("hi")),"<b>"),
           doc(blockquote("hi","<a>"),"<b>"),
           "invalid content");
}