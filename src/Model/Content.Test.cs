using System.Text.Json;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;


namespace StepWise.Prose.Model.Test;

using static TestBuilder.Builder;

public class ProseContentTest {
    public ITestOutputHelper Out;

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    public ProseContentTest(ITestOutputHelper @out) {
        Out = @out;
    }

    [Fact] public void Accepts_Empty_Content_For_The_Empty_Expr() { Valid("", ""); }
    [Fact] public void Does_Not_Accept_Content_In_TheEmptyExpr() { Invalid("", "image"); }

    [Fact] public void Matches_Nothing_To_An_Asterik() { Valid("image*", ""); }
    [Fact] public void Matches_One_Element_To_An_Asterik() { Valid("image*", "image"); }
    [Fact] public void Matches_Multiple_Elements_To_An_Asterik() { Valid("image*", "image image image image"); }
    [Fact] public void Only_Matches_Appropriate_Elements_To_An_Asterik() { Invalid("image*", "image text"); }

    [Fact] public void Matches_Group_Members_To_A_Group() { Valid("inline*", "image text"); }
    [Fact] public void Does_Not_Match_NonMembers_To_A_Group() { Invalid("inline*", "paragraph"); }
    [Fact] public void Matches_An_Element_To_A_Choice_Expression() { Valid("(paragraph | heading)", "paragraph"); }
    [Fact] public void Does_Not_Match_Unmentioned_Elements_To_A_Choice_Expr() { Invalid("(paragraph | heading)", "image"); }

    [Fact] public void Matches_A_Simple_Sequence() { Valid("paragraph horizontal_rule paragraph", "paragraph horizontal_rule paragraph"); }
    [Fact] public void Fails_When_A_Sequence_Is_Too_Long() { Invalid("paragraph horizontal_rule", "paragraph horizontal_rule paragraph"); }
    [Fact] public void Fails_When_A_Sequence_Is_Too_Short() { Invalid("paragraph horizontal_rule paragraph", "paragraph horizontal_rule"); }
    [Fact] public void Fails_When_A_Sequence_Starts_Incorrectly() { Invalid("paragraph horizontal_rule", "horizontal_rule paragraph horizontal_rule"); }

    [Fact] public void Accepts_A_Sequence_Asterisk_Matching_Zero_Elements() { Valid("heading paragraph*", "heading"); }
    [Fact] public void Accepts_A_Sequence_Asterisk_Matching_Multiple_Elts() { Valid("heading paragraph*", "heading paragraph paragraph"); }
    [Fact] public void Accepts_A_Sequence_Plus_Matching_One_Element() { Valid("heading paragraph+", "heading paragraph"); }
    [Fact] public void Accepts_A_Sequence_Plus_Matching_Multiple_Elts() { Valid("heading paragraph+", "heading paragraph paragraph"); }
    [Fact] public void Fails_When_A_Sequence_Plus_Has_No_Elements() { Invalid("heading paragraph+", "heading"); }
    [Fact] public void Fails_When_A_Sequence_Plus_Misses_Its_Start() { Invalid("heading paragraph+", "paragraph paragraph"); }

    [Fact] public void Accepts_An_Optional_Element_Being_Present() { Valid("image?", "image"); }
    [Fact] public void Accepts_An_Optional_Element_Being_Missing() { Valid("image?", ""); }
    [Fact] public void Fails_When_An_Optional_Element_Is_Present_Twice() { Invalid("image?", "image image"); }

    [Fact] public void Accepts_A_Nested_Repeat() { Valid("(heading paragraph+)+", "heading paragraph heading paragraph paragraph"); }
    [Fact] public void Fails_On_Extra_Input_After_A_Nested_Repeat() {
        Invalid("(heading paragraph+)+", "heading paragraph heading paragraph paragraph horizontal_rule");
    }

    [Fact] public void Accepts_A_Matching_Count() { Valid("hard_break{2}", "hard_break hard_break"); }
    [Fact] public void Rejects_A_Count_That_Comes_Up_Short() { Invalid("hard_break{2}", "hard_break"); }
    [Fact] public void Rejects_A_Count_That_Has_Too_Many_Elements() { Invalid("hard_break{2}", "hard_break hard_break hard_break"); }
    [Fact] public void Accepts_A_Count_On_The_Lower_Bound() { Valid("hard_break{2, 4}", "hard_break hard_break"); }
    [Fact] public void Accepts_A_Count_On_The_Upper_Bound() { Valid("hard_break{2, 4}", "hard_break hard_break hard_break hard_break"); }
    [Fact] public void Accepts_A_Count_Between_The_Bounds() { Valid("hard_break{2, 4}", "hard_break hard_break hard_break"); }
    [Fact] public void Rejects_A_Sequence_With_Too_Few_Elements() { Invalid("hard_break{2, 4}", "hard_break"); }
    [Fact] public void Rejects_A_Sequence_With_Too_Many_Elements() { Invalid("hard_break{2, 4}", "hard_break hard_break hard_break hard_break hard_break"); }
    [Fact] public void Rejects_A_Sequence_With_A_Bad_Element_After_It() { Invalid("hard_break{2, 4} text*", "hard_break hard_break image"); }
    [Fact] public void Accepts_A_Sequence_With_A_Matching_Element_After_It() { Valid("hard_break{2, 4} image?", "hard_break hard_break image"); }
    [Fact] public void Accepts_An_Open_Range() { Valid("hard_break{2,}", "hard_break hard_break"); }
    [Fact] public void Accepts_An_Open_Range_Matching_Many() { Valid("hard_break{2,}", "hard_break hard_break hard_break hard_break"); }
    [Fact] public void Rejects_An_Open_Range_With_Too_Few() { Invalid("hard_break{2,}", "hard_break"); }

    [Fact] public void Returns_The_Empty_Fragment_When_Things_Match() {
        Fill("paragraph horizontal_rule paragraph", doc(p(),hr()), doc(p()), doc()); }
    [Fact] public void Adds_A_Node_When_Necessary() {
        Fill("paragraph horizontal_rule paragraph", doc(p()), doc(p()), doc(hr())); }
    [Fact] public void Accepts_An_Asterisk_Across_The_Bound() { Fill("hard_break*", p(br()), p(br()), p()); }
    [Fact] public void Accepts_An_Asterisk_On_The_Left() { Fill("hard_break*", p(br()), p(), p()); }
    [Fact] public void Accepts_An_Asterisk_On_The_Right() { Fill("hard_break*", p(), p(br()), p()); }
    [Fact] public void Accepts_An_Asterisk_With_No_Elements() { Fill("hard_break*", p(), p(), p()); }
    [Fact] public void Accepts_A_Plus_Across_The_Bound() { Fill("hard_break+", p(br()), p(br()), p()); }
    [Fact] public void Adds_An_Element_For_A_ContentLess_Plus() { Fill("hard_break+", p(), p(), p(br())); }
    [Fact] public void Fails_For_A_Missmatched_Plus() { Fill("hard_break+", p(), p(img()), null); }
    [Fact] public void Accepts_An_Asterisk_With_Content_On_Both_Sides() { Fill("heading* paragraph*", doc(h1()), doc(p()), doc()); }
    [Fact] public void Accepts_An_Asterisk_With_No_Content_After() { Fill("heading* paragraph*", doc(h1()), doc(), doc()); }
    [Fact] public void Accepts_A_Plus_With_Content_On_Both_Sides() { Fill("heading+ paragraph+", doc(h1()), doc(p()), doc()); }
    [Fact] public void Accepts_A_Plus_With_No_Content_After() { Fill("heading+ paragraph+", doc(h1()), doc(), doc(p())); }
    [Fact] public void Adds_Elements_To_Match_A_Count() { Fill("hard_break{3}", p(br()), p(br()), p(br())); }
    [Fact] public void Fails_When_There_Are_Too_Many_Elements() { Fill("hard_break{3}", p(br(), br()), p(br(), br()), null); }
    [Fact] public void Adds_Elements_For_Two_Counted_Groups() { Fill("code_block{2} paragraph{2}", doc(pre()), doc(p()), doc(pre(), p())); }
    [Fact] public void Does_Not_Include_Optional_Elements() { Fill("heading paragraph? horizontal_rule", doc(h1()), doc(), doc(hr())); }

    [Fact] public void Completes_A_Sequence() {
        Fill3("paragraph horizontal_rule paragraph horizontal_rule paragraph",
             doc(p()), doc(p()), doc(p()), doc(hr()), doc(hr())); }

    [Fact] public void Accepts_Plus_Across_Two_Bounds() {
        Fill3("code_block+ paragraph+",
             doc(pre()), doc(pre()), doc(p()), doc(), doc()); }

    [Fact] public void Fills_A_Plus_From_Empty_Input() {
        Fill3("code_block+ paragraph+",
             doc(), doc(), doc(), doc(), doc(pre(), p())); }

    [Fact] public void Completes_A_Count() {
        Fill3("code_block{3} paragraph{3}",
             doc(pre()), doc(p()), doc(), doc(pre(), pre()), doc(p(), p())); }

    [Fact] public void Fails_On_Non_Matching_Elements() {
        Fill3("paragraph*", doc(p()), doc(pre()), doc(p()), null); }

    [Fact] public void Completes_A_Plus_Across_Two_Bounds() {
        Fill3("paragraph{4}", doc(p()), doc(p()), doc(p()), doc(), doc(p())); }

    [Fact] public void Refuses_To_Complete_An_Overflown_Count_Across_Two_Bounds() {
        Fill3("paragraph{2}", doc(p()), doc(p()), doc(p()), null); }

    private void Valid(string expr, string types) {
        Match(expr, types, schema).Should().BeTrue();
    }

    private void Fill(string expr, Node before, Node after, Node? result) {
        var filled = ContentMatch.Parse(expr, schema.Nodes).MatchFragment(before.Content)!.FillBefore(after.Content, true);
        if (result is not null) filled!.Eq(result.Content).Should().BeTrue();
        else filled.Should().BeNull();
    }

    private void Fill3(string expr, Node before, Node mid, Node after, Node? left, Node? right = null){
        var content = ContentMatch.Parse(expr, schema.Nodes);
        var a = content.MatchFragment(before.Content)!.FillBefore(mid.Content);
        var b = a is not null ? content.MatchFragment(before.Content.Append(a).Append(mid.Content))!.FillBefore(after.Content, true) : null;
        if (left is not null) {
            a!.Eq(left.Content).Should().BeTrue();
            b!.Eq(right!.Content).Should().BeTrue();
        } else {
            b.Should().BeNull();
        }
    }

    private void Invalid(string expr, string types) {
        Match(expr, types, schema).Should().BeFalse();
    }

    private bool Match(string expr, string types, Schema schema) {
        var m = ContentMatch.Parse(expr, schema.Nodes);
        var ts = types != string.Empty ? types.Split(" ").Select(t => schema.Nodes.TryGetValue(t, out var type) ? type : null).ToList() : new List<NodeType?>();
        for (var i = 0; m is not null && i < ts.Count; i++) m = m.MatchType(ts[i]!);
        return m is not null && m.ValidEnd;
    }
}