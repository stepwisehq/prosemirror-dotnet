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

```csharp
using StepWise.Prose.Model;
using StepWise.Prose.TestBuilder;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Collab.Test;

using static StepWise.Prose.TestBuilder.Builder;

Node node = doc(p("Hello <a>"));

var tr = new Transform.Transform(node);

tr.ReplaceWith(node.Tag()["a"],node.Tag()["a"],schema.Text("World"));

Console.WriteLine(tr.Doc); // doc(paragraph("Hello World"))
```