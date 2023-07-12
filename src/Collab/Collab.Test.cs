using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Collab.Test;

using static Builder;
using static StepWise.Prose.Test.TestUtils;

public class CollabTest {
    public ITestOutputHelper Out;

    public CollabTest(ITestOutputHelper @out) {
        Out = @out;
    }

    [Fact] public void Recovers_Replace_To_Position() {
        var node = doc(p("#"));

        var transform = new Transform(node);

        transform.ReplaceWith(2,2,schema.Text("#"));
        transform.Replace(1,3, Slice.Empty);

        var (newDoc, commit) = Collab.ApplyCommit(1, node, new List<Commit>(), new Commit(
            1, "abc", transform.Steps
        ));

        ist(newDoc, doc(p()), eq);
    }
}