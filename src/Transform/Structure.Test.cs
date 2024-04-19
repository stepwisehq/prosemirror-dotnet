using System.Text.Json;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Collections;
using StepWise.Prose.Model;
using StepWise.Prose.SchemaBasic;


namespace StepWise.Prose.Transformation.Test;

using static Structure;

public class StructureTest {
    public ITestOutputHelper Out;

    public static Schema schema { get; } = new(new(){
        Nodes = new() {
            ["doc"] = new() { Content = "head? block* sect* closing?"},
            ["para"] = new() { Content = "text*", Group = "block"},
            ["head"] = new() { Content = "text*", Marks = ""},
            ["figure"] = new() { Content = "caption figureimage", Group = "block"},
            ["quote"] = new() { Content = "block+", Group = "block"},
            ["figureimage"] = new() {},
            ["caption"] = new() { Content = "text*", Marks = ""},
            ["sect"] = new() { Content = "head block* sect*"},
            ["closing"] = new() { Content = "text*"},
            ["text"] = BasicSchema.Schema.Spec.Nodes["text"]!,
            ["fixed"] = new() {Content = "head para closing", Group = "block"}
        },
        Marks = new() {
            ["em"] = new()
        }
    });

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    public StructureTest(ITestOutputHelper @out) {
        Out = @out;
    }

    public static Node n(string name, params Node[] content) =>
        schema.Nodes[name].Create(null, content.ToList());
    public static Node t(string str, bool em = false) =>
        schema.Text(str, em ? new() {schema.Mark("em")} : null);

    public static Node doc { get; } = n("doc", // 0
              n("head", t("Head")), // 6
              n("para", t("Intro")), // 13
              n("sect", // 14
                n("head", t("Section head")), // 28
                n("sect", // 29
                  n("head", t("Subsection head")), // 46
                  n("para", t("Subtext")), // 55
                  n("figure", // 56
                    n("caption", t("Figure caption")), // 72
                    n("figureimage")), // 74
                  n("quote", n("para", t("!"))))), // 81
              n("sect", // 82
                n("head", t("S2")), // 86
                n("para", t("Yes"))), // 92
              n("closing", t("fin"))); // 97

    public static NodeRange? range(int pos, int? end = null) =>
        doc.Resolve(pos).BlockRange(end is null ? null : doc.Resolve(end.Value));

    private static void yes(int pos, int? depth = null, string? after = null) =>
        CanSplit(doc, pos, depth, after is null ? null : new() {new(schema.Nodes[after], null)}).Should().BeTrue();
    private static void no(int pos, int? depth = null, string? after = null) =>
        CanSplit(doc, pos, depth, after is null ? null : new() {new(schema.Nodes[after], null)}).Should().BeFalse();

    [Fact] public void Cant_At_Start() => no(0);
    [Fact] public void Cant_In_Head() => no(3);
    [Fact] public void Can_By_Making_Head_A_Para() => yes(3, 1, "para");
    [Fact] public void Cant_On_Top_Level() => no(6);
    [Fact] public void Can_In_Regular_Para() => yes(8);
    [Fact] public void Cant_At_Start_Of_Section() => no(14);
    [Fact] public void Cant_In_Section_Head() => no(17);
    [Fact] public void Can_If_Also_Splitting_The_Section() => yes(17, 2);
    [Fact] public void Can_If_Making_The_Remaining_Head_A_Para() => yes(18, 1, "para");
    [Fact] public void Cant_After_The_Section_Head() => no(46);
    [Fact] public void Can_In_The_First_Section_Para() => yes(48);
    [Fact] public void Cant_In_The_Figure_Caption() => no(60);
    [Fact] public void Cant_If_It_Also_Splits_The_Figure() => no(62,2);
    [Fact] public void Cant_After_The_Figure_Caption() => no(72);
    [Fact] public void Can_In_The_First_Para_In_A_Quote() => yes(76);
    [Fact] public void Can_If_It_Also_Splits_The_Quote() => yes(77,2);
    [Fact] public void Cant_At_The_End_Of_The_Document() => no(97);

    [Fact] public void Doesnt_Return_True_When_The_Split_Off_Content_Doesnt_Fit_In_The_Given_Node_Type() {
        var nodes = new OrderedDictionary<string, NodeSpec>(schema.Spec.Nodes);
        nodes.Add("title", new() {Content = "text*"});
        nodes.Add("chapter", new() {Content = "title scene+"});
        nodes.Add("scene", new() {Content = "para+"});
        nodes.SetValue("doc", new() {Content = "chapter+"});

        var s = new Schema(new() {Nodes = nodes});
        CanSplit(s.Node("doc", null, s.Node("chapter", null, [
            s.Node("title", null, s.Text("title")),
            s.Node("scene", null, s.Node("para", null, s.Text("scene")))
        ])), 4, 1, new(){new(s.Nodes["scene"], null)}).Should().BeFalse();
    }

    private static void yesLift(int pos) {
        var r = range(pos); (r is not null && LiftTarget(r) > 0).Should().BeTrue(); }
    private static void noLift(int pos) {
        var r = range(pos); (r is not null && LiftTarget(r) > 0).Should().BeFalse(); }

    [Fact] public void Cant_At_The_Start_Of_The_Doc() => noLift(0);
    [Fact] public void Cant_In_The_Heading() => noLift(3);
    [Fact] public void Cant_In_A_Subsection_Para() => noLift(52);
    [Fact] public void Cant_In_A_Figure_Caption() => noLift(70);
    [Fact] public void Can_From_A_Quote() => yesLift(76);
    [Fact] public void Cant_In_A_Section_Head() => noLift(86);

    private static void yesWrap(int pos, int end, string type) {
        var r = range(pos, end); (r is not null && FindWrapping(r, schema.Nodes[type]) is not null).Should().BeTrue(); }
    private static void noWrap(int pos, int end, string type) {
        var r = range(pos, end); (r is not null && FindWrapping(r, schema.Nodes[type]) is not null).Should().BeFalse(); }

    [Fact] public void Can_Wrap_The_Whole_Doc_In_A_Section() => yesWrap(0, 92, "sect");
    [Fact] public void Cant_Wrap_A_Head_Before_A_Para_In_A_Section() => noWrap(4, 4, "sect");
    [Fact] public void Can_Wrap_A_Top_Paragraph_In_A_Quote() => yesWrap(8, 8, "quote");
    [Fact] public void Cant_Wrap_A_Section_Head_In_A_Quote() => noWrap(18, 18, "quote");
    [Fact] public void Can_Wrap_A_Figure_In_A_Quote() => yesWrap(55, 74, "quote");
    [Fact] public void Cant_Wrap_A_Head_In_A_Figure() => noWrap(90, 90, "figure");

    private static void repl(Node doc, int from, int to, Node? content, int openStart, int openEnd, Node result) {
        var slice = content is not null ? new Slice(content.Content, openStart, openEnd) : Slice.Empty;
        var tr = new Transform(doc).Replace(from, to, slice);
        tr.Doc.Eq(result).Should().BeTrue();
    }

   [Fact] public void Automatically_Adds_A_Heading_To_A_Section()  =>
      repl(n("doc", n("sect", n("head", t("foo")), n("para", t("bar")))),
            6, 6, n("doc", n("sect"), n("sect")), 1, 1,
            n("doc", n("sect", n("head", t("foo"))), n("sect", n("head"), n("para", t("bar")))));

   [Fact] public void Suppresses_Impossible_Inputs()  =>
      repl(n("doc", n("para", t("a")), n("para", t("b"))),
            3, 3, n("doc", n("closing", t("."))), 0, 0,
            n("doc", n("para", t("a")), n("para", t("b"))));

   [Fact] public void Adds_Necessary_Nodes_To_The_Left()  =>
      repl(n("doc", n("sect", n("head", t("foo")), n("para", t("bar")))),
            1, 3, n("doc", n("sect"), n("sect", n("head", t("hi")))), 1, 2,
            n("doc", n("sect", n("head")), n("sect", n("head", t("hioo")), n("para", t("bar")))));

   [Fact] public void Adds_A_Caption_To_A_Figure()  =>
      repl(n("doc"),
            0, 0, n("doc", n("figure", n("figureimage"))), 1, 0,
            n("doc", n("figure", n("caption"), n("figureimage"))));

   [Fact] public void Adds_An_Image_To_A_Figure()  =>
      repl(n("doc"),
            0, 0, n("doc", n("figure", n("caption"))), 0, 1,
            n("doc", n("figure", n("caption"), n("figureimage"))));

   [Fact] public void Can_Join_Figures()  =>
      repl(n("doc", n("figure", n("caption"), n("figureimage")), n("figure", n("caption"), n("figureimage"))),
            3, 8, null, 0, 0,
            n("doc", n("figure", n("caption"), n("figureimage"))));

   [Fact] public void Adds_Necessary_Nodes_To_A_Parent_Node()  =>
      repl(n("doc", n("sect", n("head"), n("figure", n("caption"), n("figureimage")))),
            7, 9, n("doc", n("para", t("hi"))), 0, 0,
            n("doc", n("sect", n("head"), n("figure", n("caption"), n("figureimage")), n("para", t("hi")))));
}