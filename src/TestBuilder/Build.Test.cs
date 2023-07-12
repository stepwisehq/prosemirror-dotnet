using System.Text.Json;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using StepWise.Prose.Model;
using StepWise.Prose.SchemaBasic;


namespace StepWise.Prose.TestBuilder.Test;

using static StepWise.Prose.TestBuilder.Builder;

public class TestBuilderTest : IDisposable {
    public ITestOutputHelper Out;

    public static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true};

    private static string SchemaJson = """{"nodes":{"doc":{"content":"block+"},"paragraph":{"content":"inline*","group":"block","parseDOM":[{"tag":"p"}]},"blockquote":{"content":"block+","group":"block","defining":true,"parseDOM":[{"tag":"blockquote"}]},"horizontal_rule":{"group":"block","parseDOM":[{"tag":"hr"}]},"heading":{"attrs":{"level":{"default":1}},"content":"inline*","group":"block","defining":true,"parseDOM":[{"tag":"h1","attrs":{"level":1}},{"tag":"h2","attrs":{"level":2}},{"tag":"h3","attrs":{"level":3}},{"tag":"h4","attrs":{"level":4}},{"tag":"h5","attrs":{"level":5}},{"tag":"h6","attrs":{"level":6}}]},"code_block":{"content":"text*","marks":"","group":"block","code":true,"defining":true,"parseDOM":[{"tag":"pre","preserveWhitespace":"full"}]},"text":{"group":"inline"},"image":{"inline":true,"attrs":{"src":{},"alt":{"default":null},"title":{"default":null}},"group":"inline","draggable":true,"parseDOM":[{"tag":"img[src]"}]},"hard_break":{"inline":true,"group":"inline","selectable":false,"parseDOM":[{"tag":"br"}]},"ordered_list":{"attrs":{"order":{"default":1}},"parseDOM":[{"tag":"ol"}],"content":"list_item+","group":"block"},"bullet_list":{"parseDOM":[{"tag":"ul"}],"content":"list_item+","group":"block"},"list_item":{"parseDOM":[{"tag":"li"}],"defining":true,"content":"paragraph block*"}},"marks":{"link":{"attrs":{"href":{},"title":{"default":null}},"inclusive":false,"parseDOM":[{"tag":"a[href]"}]},"em":{"parseDOM":[{"tag":"i"},{"tag":"em"},{"style":"font-style=italic"},{"style":"font-style=normal"}]},"strong":{"parseDOM":[{"tag":"strong"},{"tag":"b"},{"style":"font-weight=400"},{"style":"font-weight"}]},"code":{"parseDOM":[{"tag":"code"}]}}}""";
    private Schema schema { get; init; }

    public TestBuilderTest(ITestOutputHelper @out) {
        Out = @out;
        var schemaSpec = JsonSerializer.Deserialize<SchemaSpec>(SchemaJson, JsonOptions);
        schema = new Schema(schemaSpec!);
        Builder.schema = new Schema(new() {
            Nodes = new() {
                ["doc"] = new() {
                    Content = "block+"
                },
                ["paragraph"] = new() {
                    Content = "inline*",
                    Group = "block",
                },
                ["text"] = new() {
                    Group = "inline"
                }
            },
            Marks = new() {
                ["link"] = new() {
                    Attrs = new() {
                        ["href"] = new()
                    },
                    Excludes = ""
                }
            }
        });
    }

    [Fact]
    public void Deduplicates_Identical_Marks() {
        var actual = doc(p(a(new {href = "/foo"}, a(new {href = "/foo"}, "click <p>here"))));
        var expected = doc(p(a(new {href = "/foo"}, "click <p>here")));

        expected.Eq(actual).Should().BeTrue();
        actual.NodeAt(actual.Tag()["p"])!.Marks.Count.Should().Be(1);
    }

    [Fact]
    public void Marks_Of_Same_Type_But_Different_Attributes_Are_Distinct() {
        var actual = doc(p(a(new {href = "/foo"}, a(new{href = "/bar"}, "click <p>here"))));

        actual.NodeAt(actual.Tag()["p"])!.Marks.Count.Should().Be(2);
    }

    public void Dispose() {
        Builder.schema = BasicSchema.Schema;
    }
}