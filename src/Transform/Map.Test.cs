using System.Text.Json;

using FluentAssertions;
using OneOf;
using Xunit;
using Xunit.Abstractions;


namespace StepWise.Prose.Transformation.Test;

using Transformation;

using MappingCase = OneOf<
    (int from, int to),
    (int from, int to, int bias),
    (int from, int to, int bias, bool lossy)
>;

# pragma warning disable CS8981
using d = Dictionary<int, int>;


public class MappingTest {


    public ITestOutputHelper Out;

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    public MappingTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private void testMapping(Mapping mapping, params MappingCase[] cases) {
        var inverted = mapping.Invert();
        for (var i = 0; i < cases.Length; i++) {
            var tCase = cases[i];
            tCase.Switch(
                a => {
                    var (from, to) = a;
                    mapping.Map(from, 1).Should().Be(to);
                    inverted.Map(to, 1).Should().Be(from);
                },
                b => {
                    var (from, to, bias) = b;
                    mapping.Map(from, bias).Should().Be(to);
                    inverted.Map(to, bias).Should().Be(from);
                },
                c => {
                    var (from, to, bias, lossy) = c;
                    mapping.Map(from, bias).Should().Be(to);
                    if (!lossy) inverted.Map(to, bias).Should().Be(from);
                }
            );
        }
    }

    private void testDel(Mapping mapping, int pos, int side, string flags) {
        var r = mapping.MapResult(pos, side);
        var found = "";
        if (r.Deleted) found += "d";
        if (r.DeletedBefore) found += "b";
        if (r.DeletedAfter) found += "a";
        if (r.DeletedAcross) found += "x";
        found.Should().Be(flags);
    }

    public Mapping mk(params OneOf<(int, int, int), Dictionary<int, int>>[] args) {
        var mapping = new Mapping();
        foreach (var arg in args) {
            arg.Switch(
                t => mapping.AppendMap(new StepMap(new() {t.Item1, t.Item2, t.Item3})),
                dict => {
                    foreach (var (from, to) in dict) mapping.SetMirror(+from, to);
                }
            );
        }
        return mapping;
    }

    [Fact] public void Can_Map_Through_An_Insertion() =>
        testMapping(mk((2, 0, 1)), (2, 3));

    [Fact] public void Can_Map_Through_A_Single_Insertion() =>
        testMapping(mk((2, 0, 4)), (0, 0), (2, 6), (2, 2, -1), (3, 7));

    [Fact] public void Can_Map_Through_A_Single_Deletion() =>
        testMapping(mk((2, 4, 0)), (0, 0), (2, 2, -1), (3, 2, 1, true), (6, 2, 1), (6, 2, -1, true), (7, 3));

    [Fact] public void Can_Map_Through_A_Single_Replace() =>
        testMapping(mk((2, 4, 4)), (0, 0), (2, 2, 1), (4, 6, 1, true), (4, 2, -1, true), (6, 6, -1), (8, 8));

    [Fact] public void Can_Map_Through_Mirrored_DeleteInsert() =>
        testMapping(mk((2, 4, 0), (2, 0, 4), new d{[0] = 1}), (2, 2), (4, 4), (6, 6), (7, 7));

    [Fact] public void Can_Map_Through_A_Mirrored_InsertDelete() =>
        testMapping(mk((2, 0, 4), (2, 4, 0), new d{[0] = 1}), (0, 0), (2, 2), (3, 3));

    [Fact] public void Can_Map_Through_An_DeleteInsert_With_An_Insert_In_Between() =>
        testMapping(mk((2, 4, 0), (1, 0, 1), (3, 0, 4), new d{[0] = 2}), (0, 0), (1, 2), (4, 5), (6, 7), (7, 8));

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Before() {
        testDel(mk((0, 2, 0)), 2, -1, "db");
        testDel(mk((0, 2, 0)), 2, 1, "b");
        testDel(mk((0, 2, 2)), 2, -1, "db");
        testDel(mk((0, 1, 0), (0, 1, 0)), 2, -1, "db");
        testDel(mk((0, 1, 0)), 2, -1, "");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flgs_When_Deletion_Happens_After() {
        testDel(mk((2, 2, 0)), 2, -1, "a");
        testDel(mk((2, 2, 0)), 2, 1, "da");
        testDel(mk((2, 2, 2)), 2, 1, "da");
        testDel(mk((2, 1, 0), (2, 1, 0)), 2, 1, "da");
        testDel(mk((3, 2, 0)), 2, -1, "");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Accross() {
        testDel(mk((0, 4, 0)), 2, -1, "dbax");
        testDel(mk((0, 4, 0)), 2, 1, "dbax");
        testDel(mk((0, 4, 0)), 2, 1, "dbax");
        testDel(mk((0, 1, 0), (4, 1, 0), (0, 3, 0)), 2, 1, "dbax");
    }

    [Fact] public void Assigns_The_Correct_Deleted_Flags_When_Deletions_Happen_Around() {
        testDel(mk((4, 1, 0), (0, 1, 0)), 2, -1, "");
        testDel(mk((2, 1, 0), (0, 2, 0)), 2, -1, "dba");
        testDel(mk((2, 1, 0), (0, 1, 0)), 2, -1, "a");
        testDel(mk((3, 1, 0), (0, 2, 0)), 2, -1, "db");
    }


}