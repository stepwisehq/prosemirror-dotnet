using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation.Test;

using static StepWise.Prose.TestBuilder.Builder;
using static StepWise.Prose.Test.TestUtils;

public class ReplaceStepTest {
    public ITestOutputHelper Out;

    public ReplaceStepTest(ITestOutputHelper @out) {
        Out = @out;
    }

    private void test(Node doc, Action<Transform> change, Action<Transform> otherChange, Node expected) {
        var trA = new Transform(doc);
        var trB = new Transform(doc);
        change(trA);
        otherChange(trB);
        var result = new Transform(trB.Doc).Step(trA.Steps[0].Map(trB.Mapping)!).Doc;
        ist(result, expected, eq);
    }

    [Fact] public void Doesnt_Break_Wrap_Steps_On_Insert() =>
        test(doc(p("a")),
            tr => tr.Wrap(tr.Doc.Resolve(1).BlockRange()!, [new(schema.Nodes["blockquote"], [])]),
            tr => tr.Insert(0, p("b")),
            doc(p("b"), blockquote(p("a"))));

    [Fact] public void Doesnt_Overwrite_Content_Inserted_At_Start_Of_Unwrap_Step() =>
        test(doc(blockquote(p("a"))),
            tr => tr.Lift(tr.Doc.Resolve(2).BlockRange()!, 0),
            tr => tr.Insert(2, schema.Text("x")),
            doc(p("xa")));
}
