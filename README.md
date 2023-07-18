ProseMirror.Net
===============

A direct translation of the core ProseMirror packages from TypeScript to C#

- [x] `prosemirror-model`
- [x] `prosemirror-transform`
- [x] `prosemirror-test-builder`
- [x] `prosemirror-schema-basic`
- [x] `prosemirror-schema-list`


## Getting Started

```bash
dotnet add package StepWise.ProseMirror
```

### Basic Usage

```csharp
using StepWise.Prose.Model;

var schemaSpec = new SchemaSpec() {
	Nodes = new()
	{
		["doc"] = new() { Content = "paragraph+" },
		["paragraph"] = new() { Content = "text*" },
		["text"] = new() { }
	},
    Marks = new()
};
var schema = new Schema(schemaSpec);

var doc = schema.Node("doc", null, new NodeList {
	schema.Node("paragraph", null, new NodeList {
		schema.Text("Hello World")
	}, null)
}, null);

Console.WriteLine(doc.ToString()); // doc(paragraph("Hello World"))
```


### Test Builder & Transform

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;

using static StepWise.Prose.TestBuilder.Builder;

Node node = doc(p("Hello <a>"));

var tr = new Transform(node);

tr.ReplaceWith(node.Tag()["a"], node.Tag()["a"], schema.Text("World"));

Console.WriteLine(tr.Doc.ToString()); // doc(paragraph("Hello World"))
```

### Deserializing Steps

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;

using static StepWise.Prose.TestBuilder.Builder;

Node node = doc(p("Hello <a>"));

var stepJsonString = """{"stepType":"replace","from":7,"to":7,"slice":{"content":[{"type":"text","text":"World","marks":[]}]}}""";
var step = Step.FromJSON(Builder.schema, StepDto.FromJson(stepJsonString));

var tr = new Transform(node);
tr.Step(step);

Console.WriteLine(tr.Doc.ToString()); // doc(paragraph("Hello World"))
```

### Deserializing Docs(Nodes)
```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;

var docJsonString = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Hello World","marks":[]}],"marks":[]}],"marks":[]}""";
var doc = Node.FromJSON(Builder.schema, NodeDto.FromJson(docJsonString));

Console.WriteLine(doc.ToString()); // doc(paragraph("Hello World"))
```