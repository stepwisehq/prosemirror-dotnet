Test Builder
============

This project contains classes useful for building and testing Nodes and Marks. These are used heavily in the ProseMirror
test suites.

It is based on the [prosemirror-test-builders](https://github.com/ProseMirror/prosemirror-test-builder) project.


## Builder

The builder methods are static methods on the `Builder` class; ie `p()` `doc()` and etc.

These are hard-coded for convenient code completion as C# lacks the typing flexibility of TypeScript, and
code generators were out-of-scope.

### Usage

```csharp
using static StepWise.Prose.TestBuilder.Builder;

# ...

doc(p("Hello world"));
```

### Warning: Schema is AsyncLocal

We leverage `using static` to achieve a syntax as close to the TypeScript project's as possible. These static methods
use the static `Builder.schema`. This *can* be changed however be aware that it pulls from an `AsyncLocal<Schema>` variable
which means all future builder method calls within the same async context will start using this schema.

Alteratively, you can use the builder helpers outlined bellow to bind a Schema to a set of builder delegates.

## Builders Helper

You can also call `Builder.BuildersDynamic` to create builder delegates bound to the
supplied `Schema`.

The return object is `dynamic` for convience so one can call `return.doc()`. However,
this convenience is a trade-off to match the TypeScript project's syntax at the cost of compile-time safety.