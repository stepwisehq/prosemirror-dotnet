using System.Text.Json;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.TestBuilder;


namespace StepWise.Prose.Model.Test;

using static Builder;

public class DiffTest {
    public ITestOutputHelper Out;

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    public DiffTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static void start(Node a, Node b) {
        int? tag = a.Tag().TryGetValue("a", out var aTag) ? aTag : null;
        a.Content.FindDiffStart(b.Content).Should().Be(tag);
    }

    [Fact] public void Returns_Null_For_Identical_Nodes() {
        start(doc(p("a", em("b")), p("hello"), blockquote(h1("bye"))),
             doc(p("a", em("b")), p("hello"), blockquote(h1("bye")))); }

    [Fact] public void Notices_When_One_Node_Is_Longer() {
        start(doc(p("a", em("b")), p("hello"), blockquote(h1("bye")), "<a>"),
             doc(p("a", em("b")), p("hello"), blockquote(h1("bye")), p("oops"))); }

    [Fact] public void Notices_When_One_Node_Is_Shorter() {
        start(doc(p("a", em("b")), p("hello"), blockquote(h1("bye")), "<a>", p("oops")),
             doc(p("a", em("b")), p("hello"), blockquote(h1("bye")))); }

    [Fact] public void Notices_Differing_Marks() {
        start(doc(p("a<a>", em("b"))),
             doc(p("a", strong("b")))); }

    [Fact] public void Stops_At_Longer_Text() {
        start(doc(p("foo<a>bar", em("b"))),
             doc(p("foo", em("b")))); }

    [Fact] public void Stops_At_A_Different_Character() {
        start(doc(p("foo<a>bar")),
             doc(p("foocar"))); }

    [Fact] public void Stops_At_A_Different_Node_Type() {
        start(doc(p("a"), "<a>", p("b")),
             doc(p("a"), h1("b"))); }

    [Fact] public void Works_When_The_Difference_Is_At_The_Start() {
        start(doc("<a>", p("b")),
             doc(h1("b"))); }

    [Fact] public void Notices_A_Different_Attribute() {
        start(doc(p("a"), "<a>", h1("foo")),
             doc(p("a"), h2("foo"))); }


    private static void end(Node a, Node b) {
        int? tag = a.Tag().TryGetValue("a", out var aTag) ? aTag : null;
        a.Content.FindDiffEnd(b.Content)?.a!.Should().Be(tag);
    }

    [Fact] public void Returns_Null_When_There_Is_No_Difference() {
        end(doc(p("a", em("b")), p("hello"), blockquote(h1("bye"))),
           doc(p("a", em("b")), p("hello"), blockquote(h1("bye")))); }

    [Fact] public void Notices_When_The_Second_Doc_Is_Longer() {
        end(doc("<a>", p("a", em("b")), p("hello"), blockquote(h1("bye"))),
           doc(p("oops"), p("a", em("b")), p("hello"), blockquote(h1("bye")))); }

    [Fact] public void Notices_When_The_Second_Doc_Is_Shorter() {
        end(doc(p("oops"), "<a>", p("a", em("b")), p("hello"), blockquote(h1("bye"))),
           doc(p("a", em("b")), p("hello"), blockquote(h1("bye")))); }

    [Fact] public void Notices_Different_Styles() {
        end(doc(p("a", em("b"), "<a>c")),
           doc(p("a", strong("b"), "c"))); }

    [Fact] public void Spots_Longer_Text() {
        end(doc(p("bar<a>foo", em("b"))),
           doc(p("foo", em("b")))); }

    [Fact] public void Spots_Different_Text() {
        end(doc(p("foob<a>ar")),
           doc(p("foocar"))); }

    [Fact] public void Notices_Different_Nodes() {
        end(doc(p("a"), "<a>", p("b")),
           doc(h1("a"), p("b"))); }

    [Fact] public void Notices_A_Difference_At_The_End() {
        end(doc(p("b"), "<a>"),
           doc(h1("b"))); }

    [Fact] public void Handles_A_Similar_Start() {
        end(doc("<a>", p("hello")),
           doc(p("hey"), p("hello"))); }
}