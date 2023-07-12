using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class JsonTest {
    public ITestOutputHelper Out;

    public JsonTest(ITestOutputHelper @out) {
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

   [Fact] public void Merges_Typing_Changes() {
        // yes(2, 2, "a", 3, 3, "b");

        var step = mkStep(2, 2, "a");

        var poco = step.ToJSON();

        var json = poco.ToJson();

        var newStep = StepDto.FromJson(json);

        (true).Should().BeTrue();
   }
}