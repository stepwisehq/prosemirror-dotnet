using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.TestBuilder;
using StepWise.Prose.SchemaBasic;


namespace StepWise.Prose.Model.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class MarkTest {
    public ITestOutputHelper Out;

    private record ResolveResult(Node node, int start, int end);
    private static Node testDoc { get; } = doc(p("ab"), blockquote(p(em("cd"), "ef")));
    private static ResolveResult _doc { get; } = new(testDoc, 0, 12);
    private static ResolveResult _p1 { get; } = new(testDoc.Child(0), 1, 3);
    private static ResolveResult _blk { get; } = new(testDoc.Child(1), 5, 11);
    private static ResolveResult _p2 { get; } = new(_blk.node.Child(0), 6, 10);

    public MarkTest(ITestOutputHelper @out) {
        Out = @out;
    }

    public static Mark em_ { get; } = BasicSchema.Schema.Mark("em");
    public static Mark strong { get; } = BasicSchema.Schema.Mark("strong");
    public static Func<string, string?, Mark> link { get; } = (href, title) =>
        BasicSchema.Schema.Mark("link", new() {["href"] = href, ["title"] = title});
    public static Mark code { get; } = BasicSchema.Schema.Mark("code");

    public static Schema customSchema { get; } = new Schema(new() {
        Nodes = new () {["doc"] = new() {Content = "paragraph+"}, ["paragraph"] = new() {Content = "text*"}, ["text"] = new() {}},
        Marks = new() {
            ["remark"] = new() {Attrs = new() {["id"] = new() {}}, Excludes = "", Inclusive = false},
            ["user"] = new() {Attrs = new() {["id"] = new() {}}, Excludes = "_"},
            ["strong"] = new() {Excludes = "em-group"},
            ["em"] = new() {Group = "em-group"}
        }
    });
    private static Dictionary<string, MarkType> custom { get; } = customSchema.Marks;
    private static Mark remark1 { get; } = custom["remark"].Create(new() {["id"] = 1});
    private static Mark remark2 { get; } = custom["remark"].Create(new() {["id"] = 2});
    private static Mark user1 { get; } = custom["user"].Create(new() {["id"] = 1});
    private static Mark user2 { get; } = custom["user"].Create(new() {["id"] = 2});
    private static Mark customEm { get; } = custom["em"].Create();
    private static Mark customStrong { get; } = custom["strong"].Create();

    [Fact] public void Returns_True_For_Two_Empty_Sets() => Mark.SameSet(new(), new()).Should().BeTrue();

    [Fact] public void Returns_True_For_Simple_Identical_Sets() =>
        Mark.SameSet(new() {em_, strong},new() {em_, strong}).Should().BeTrue();

    [Fact] public void Returns_False_For_Different_Sets() =>
        Mark.SameSet(new() {em_, strong}, new() {em_, code}).Should().BeFalse();

    [Fact] public void Returns_False_When_Set_Size_Differs() =>
        Mark.SameSet(new() {em_, strong}, new() {em_, strong, code}).Should().BeFalse();

    [Fact] public void Recognizes_Identical_Links_In_Set() =>
        Mark.SameSet(new() {link("http://foo", null), code}, new() {link("http://foo", null), code}).Should().BeTrue();

    [Fact] public void Recognizes_Different_Links_In_Set() =>
        Mark.SameSet(new() {link("http://foo", null), code}, new() {link("http://bar", null), code}).Should().BeFalse();

    [Fact] public void Considers_Identical_Links_To_Be_The_Same() =>
        link("http://foo", null).Eq(link("http://foo", null)).Should().BeTrue();

    [Fact] public void Consider_Different_Links_To_Differ() =>
        link("http://foo", null).Eq(link("http://bar", null)).Should().BeFalse();

    [Fact] public void Considers_Links_With_Different_Tittles_To_Differ() =>
        link("http://foo", "A").Eq(link("http://foo", "B")).Should().BeFalse();

    [Fact] public void Can_Add_To_The_Empty_Set() =>
         ist(em_.AddToSet(new()), new List<Mark>() {em_}, Mark.SameSet);

    [Fact] public void Is_A_Noop_When_The_Added_Thing_Is_In_Set() =>
        ist(em_.AddToSet(new() {em_}),new List<Mark> {em_}, Mark.SameSet);

    [Fact] public void Adds_Marks_With_Lower_Rank_Before_Others() =>
        ist(em_.AddToSet(new() {strong}),new List<Mark> {em_,strong}, Mark.SameSet);

    [Fact] public void Adds_Marks_With_Higher_Rank_After_Others() =>
        ist(strong.AddToSet(new() {em_}),new List<Mark> {em_,strong}, Mark.SameSet);

    [Fact] public void Replaces_Different_Marks_With_New_Attributes() =>
      ist(link("http://bar", null).AddToSet(new() {link("http://foo", null),em_}),
           new List<Mark> {link("http://bar", null),em_}, Mark.SameSet);

    [Fact] public void Does_Nothing_When_Adding_An_Existing_Link() =>
      ist(link("http://foo", null).AddToSet(new() {em_,link("http://foo", null)}),
           new List<Mark> {em_,link("http://foo", null)}, Mark.SameSet);

    [Fact] public void Puts_Code_Marks_At_The_End() =>
        ist(code.AddToSet(new() {em_,strong,link("http://foo", null)}),
           new List<Mark> {em_,strong,link("http://foo", null),code}, Mark.SameSet);

    [Fact] public void Puts_Marks_With_Middle_Rank_In_The_Middle() =>
        ist(strong.AddToSet(new() {em_,code}),new List<Mark> {em_,strong,code}, Mark.SameSet);

    [Fact] public void Allows_Nonexclusive_Instances_Of_Marks_With_The_Same_Type() =>
        ist(remark2.AddToSet(new() {remark1}),new List<Mark> {remark1, remark2}, Mark.SameSet);

    [Fact] public void Doesnt_Duplicate_Identical_Instances_Of_Nonexclusive_Marks() =>
        ist(remark1.AddToSet(new() {remark1}),new List<Mark> {remark1}, Mark.SameSet);

    [Fact] public void Clears_All_Others_When_Adding_A_Globally_Excluding_Mark() =>
        ist(user1.AddToSet(new() {remark1,customEm}),new List<Mark> {user1}, Mark.SameSet);

    [Fact] public void Does_Not_Allow_Adding_Another_Mark_To_A_Globally_Excluding_Mark() =>
        ist(customEm.AddToSet(new() {user1}),new List<Mark> {user1}, Mark.SameSet);

    [Fact] public void Does_Overwrite_A_Globally_Excluding_Mark_When_Adding_Another_Instance() =>
        ist(user2.AddToSet(new() {user1}),new List<Mark> {user2}, Mark.SameSet);

    [Fact] public void Doesnt_Add_Anything_When_Another_Mark_Excludes_The_Added_Mark() =>
      ist(customEm.AddToSet(new() {remark1,customStrong}),new List<Mark> {remark1, customStrong},Mark.SameSet);

    [Fact] public void Remove_Excluded_Marks_When_Adding_A_Mark() =>
      ist(customStrong.AddToSet(new() {remark1,customEm}),new List<Mark> {remark1, customStrong},Mark.SameSet);

   [Fact] public void Is_A_Noop_For_The_Empty_Set() =>
      ist(Mark.SameSet(em_.RemoveFromSet(new() {}),new() {}));

   [Fact] public void Can_Remove_The_Last_Mark_From_A_Set() =>
      ist(Mark.SameSet(em_.RemoveFromSet(new() {em_}),new() {}));

   [Fact] public void Is_A_Noop_When_The_Mark_Isnt_In_The_Set() =>
      ist(Mark.SameSet(strong.RemoveFromSet(new() {em_}),new() {em_}));

   [Fact] public void Can_Remove_A_Mark_With_Attributes() =>
      ist(Mark.SameSet(link("http://foo", null).RemoveFromSet(new() {link("http://foo", null)}),new() {}));

   [Fact] public void Doesnt_Remove_A_Mark_When_Its_Attrs_Differ() =>
      ist(Mark.SameSet(link("http://foo","title").RemoveFromSet(new() {link("http://foo", null)}),
                          new() {link("http://foo", null)}));

    private static void isAt(Node doc, Mark mark, bool result) {
        mark.IsInSet(doc.Resolve(doc.Tag()["a"]).Marks()).Should().Be(result);
    }

   [Fact] public void Recognizes_A_Mark_Exists_Inside_Marked_Text() =>
      isAt(doc(p(em("fo<a>o"))),em_,true);

   [Fact] public void Recognizes_A_Mark_Doesnt_Exist_In_Nonmarked_Text() =>
      isAt(doc(p(em("fo<a>o"))),strong,false);

   [Fact] public void Considers_A_Mark_Active_After_The_Mark() =>
      isAt(doc(p(em("hi"),"<a> there")),em_,true);

   [Fact] public void Considers_A_Mark_Inactive_Before_The_Mark() =>
      isAt(doc(p("one <a>",em("two"))),em_,false);

   [Fact] public void Considers_A_Mark_Active_At_The_Start_Of_The_Textblock() =>
      isAt(doc(p(em("<a>one"))),em_,true);

   [Fact] public void Notices_That_Attributes_Differ() =>
      isAt(doc(p(a("li<a>nk"))),link("http://baz", null),false);

    private static Node customDoc { get; } = customSchema.Node("doc", null, new NodeList{
        customSchema.Node("paragraph", null, new NodeList{ // pos 1
            customSchema.Text("one", new MarkList{remark1, customStrong}), customSchema.Text("two"),
        }),
        customSchema.Node("paragraph", null, new NodeList{ // pos 9
            customSchema.Text("one"), customSchema.Text("two", new() {remark1}), customSchema.Text("three", new() {remark1} )
        }), // pos 22
        customSchema.Node("paragraph", null, new NodeList{
            customSchema.Text("one", new() {remark2}), customSchema.Text("two", new() {remark1})
        })
    });

   [Fact] public void Omits_Non_Inclusive_Marks_At_End_Of_Mark() =>
      ist(Mark.SameSet(customDoc.Resolve(4).Marks(),new() {customStrong}));

   [Fact] public void Includes_Non_Inclusive_Marks_Inside_A_Text_Node() =>
      ist(Mark.SameSet(customDoc.Resolve(3).Marks(),new() {remark1,customStrong}));

   [Fact] public void Omits_Non_Inclusive_Marks_At_The_End_Of_A_Line() =>
      ist(Mark.SameSet(customDoc.Resolve(20).Marks(),new() {}));

   [Fact] public void Includes_Non_Inclusive_Marks_Between_Two_Marked_Nodes() =>
      ist(Mark.SameSet(customDoc.Resolve(15).Marks(),new() {remark1}));

   [Fact] public void Excludes_Non_Inclusive_Marks_At_A_Point_Where_Mark_Attrs_Change() =>
      ist(Mark.SameSet(customDoc.Resolve(25).Marks(),new() {}));

}