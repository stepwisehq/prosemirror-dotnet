using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class StepTest {
    public ITestOutputHelper Out;

    public StepTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private static Node testDoc = doc(p("foobar"));

    private static Step mkStep(int from, int to, string? val) {
        if (val == "+em")
            return new AddMarkStep(from, to, schema.Marks["em"].Create());
        else if (val == "-em")
            return new RemoveMarkStep(from, to, schema.Marks["em"].Create());
        else
            return new ReplaceStep(from, to, val is null ? Slice.Empty : new Slice(Fragment.From(schema.Text(val)), 0, 0));
    }

    private static void yes(int from1, int to1, string? val1, int from2, int to2, string? val2) {
        var step1 = mkStep(from1, to1, val1);
        var step2 = mkStep(from2, to2, val2);
        var merged = step1.Merge(step2);
        ist(merged);
        ist(merged!.Apply(testDoc).Doc!, step2.Apply(step1.Apply(testDoc).Doc!).Doc!, eq);
    }

    private static void no(int from1, int to1, string? val1, int from2, int to2, string? val2) {
        var step1 = mkStep(from1, to1, val1);
        var step2 = mkStep(from2, to2, val2);
        ist(step1.Merge(step2) is null);
    }

   [Fact] public void Merges_Typing_Changes() => yes(2, 2, "a", 3, 3, "b");

   [Fact] public void Merges_Inverse_Typing() => yes(2, 2, "a", 2, 2, "b");

   [Fact] public void Doesnt_Merge_Separated_Typing() => no(2, 2, "a", 4, 4, "b");

   [Fact] public void Doesnt_Merge_Inverted_Separated_Typing() => no(3, 3, "a", 2, 2, "b");

   [Fact] public void Merges_Adjacent_Backspaces() => yes(3, 4, null, 2, 3, null);

   [Fact] public void Merges_Adjacent_Deletes() => yes(2, 3, null, 2, 3, null);

   [Fact] public void Doesnt_Merge_Separate_Backspaces() => no(1, 2, null, 2, 3, null);

   [Fact] public void Merges_Backspace_And_Type() => yes(2, 3, null, 2, 2, "x");

   [Fact] public void Merges_Longer_Adjacent_Inserts() => yes(2, 2, "quux", 6, 6, "baz");

   [Fact] public void Merges_Inverted_Longer_Inserts() => yes(2, 2, "quux", 2, 2, "baz");

   [Fact] public void Merges_Longer_Deletes() => yes(2, 5, null, 2, 4, null);

   [Fact] public void Merges_Inverted_Longer_Deletes() => yes(4, 6, null, 2, 4, null);

   [Fact] public void Merges_Overwrites() => yes(3, 4, "x", 4, 5, "y");

   [Fact] public void Merges_Adding_Adjacent_Styles() => yes(1, 2, "+em", 2, 4, "+em");

   [Fact] public void Merges_Adding_Overlapping_Styles() => yes(1, 3, "+em", 2, 4, "+em");

   [Fact] public void Doesnt_Merge_Separate_Styles() => no(1, 2, "+em", 3, 4, "+em");

   [Fact] public void Merges_Removing_Adjacent_Styles() => yes(1, 2, "-em", 2, 4, "-em");

   [Fact] public void Merges_Removing_Overlapping_Styles() => yes(1, 3, "-em", 2, 4, "-em");

   [Fact] public void Doesnt_Merge_Removing_Separate_Styles() => no(1, 2, "-em", 3, 4, "-em");
}

