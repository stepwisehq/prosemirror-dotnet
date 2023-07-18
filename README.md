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

var tr = new Transform.Transform(node);

tr.ReplaceWith(node.Tag()["a"],node.Tag()["a"],schema.Text("World"));

Console.WriteLine(tr.Doc); // doc(paragraph("Hello World"))
```
