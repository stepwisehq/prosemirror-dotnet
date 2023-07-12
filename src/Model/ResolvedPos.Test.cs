using FluentAssertions;
using Xunit;
using Xunit.Abstractions;


namespace StepWise.Prose.Model.Test;

using X = AssertionExtensions;
using static StepWise.Prose.TestBuilder.Builder;

public class ResolvedPosTest {
    public ITestOutputHelper Out;

    public record ResolveResult(Node node, int start, int end);
    public static Node testDoc { get; } = doc(p("ab"), blockquote(p(em("cd"), "ef")));
    public static ResolveResult _doc { get; } = new(testDoc, 0, 12);
    public static ResolveResult _p1 { get; } = new(testDoc.Child(0), 1, 3);
    public static ResolveResult _blk { get; } = new(testDoc.Child(1), 5, 11);
    public static ResolveResult _p2 { get; } = new(_blk.node.Child(0), 6, 10);

    public ResolvedPosTest(ITestOutputHelper @out) {
        Out = @out;
    }

    [Fact] public void Should_Reflect_The_Document_Structure() {
        var expected = new Dictionary<int, List<dynamic?>>() {
            [0] = new() { _doc, 0, null, _p1.node },
            [1] = new() {_doc, _p1, 0, null, "ab"},
            [2] = new() {_doc, _p1, 1, "a", "b"},
            [3] = new() {_doc, _p1, 2, "ab", null},
            [4] = new() {_doc, 4, _p1.node, _blk.node},
            [5] = new() {_doc, _blk, 0, null, _p2.node},
            [6] = new() {_doc, _blk, _p2, 0, null, "cd"},
            [7] = new() {_doc, _blk, _p2, 1, "c", "d"},
            [8] = new() {_doc, _blk, _p2, 2, "cd", "ef"},
            [9] = new() {_doc, _blk, _p2, 3, "e", "f"},
            [10] = new() {_doc, _blk, _p2, 4, "ef", null},
            [11] = new() {_doc, _blk, 6, _p2.node, null},
            [12] = new() {_doc, 12, _blk.node, null}
        };

        for (var pos = 0; pos <= testDoc.Content.Size; pos++) {
            var _pos = testDoc.Resolve(pos);
            var exp = expected[pos];
            _pos.Depth.Should().Be(exp.Count - 4);
            for (var i = 0; i < exp.Count - 3; i++) {
                X.Should(_pos!.Node(i)!.Eq(exp[i]!.node)).Be(true);
                X.Should(_pos!.Start(i)).Be(exp[i]!.start);
                X.Should(_pos!.End(i)).Be(exp[i]!.end);
                if (i > 0) {
                    X.Should(_pos!.Before(i)).Be(exp[i]!.start - 1);
                    X.Should(_pos!.After(i)).Be(exp[i]!.end + 1);
                }
            }
            _pos.ParentOffset.Should().Be(exp[^3]);
            var before = _pos.NodeBefore!;
            var eBefore = exp[^2];
            if (eBefore is string) before.TextContent.Should().Be(eBefore);
            else before.Should().Be(eBefore);
            var after = _pos.NodeAfter!;
            var eAfter = exp[^1];
            if (eAfter is string) after.TextContent.Should().Be(eAfter);
            else after.Should().Be(eAfter);
        }
    }

    [Fact] public void Has_A_Working_PosAtIndex_Method() {
        var d = doc(blockquote(p("one"), blockquote(p("two ", em("three")), p("four"))));
        var pThree = d.Resolve(12); // Start of em("three")
        pThree.PosAtIndex(0).Should().Be(8);
        pThree.PosAtIndex(1).Should().Be(12);
        pThree.PosAtIndex(2).Should().Be(17);
        pThree.PosAtIndex(0, 2).Should().Be(7);
        pThree.PosAtIndex(1, 2).Should().Be(18);
        pThree.PosAtIndex(2, 2).Should().Be(24);
        pThree.PosAtIndex(0, 1).Should().Be(1);
        pThree.PosAtIndex(1, 1).Should().Be(6);
        pThree.PosAtIndex(2, 1).Should().Be(25);
        pThree.PosAtIndex(0, 0).Should().Be(0);
        pThree.PosAtIndex(1, 0).Should().Be(26);
    }
}