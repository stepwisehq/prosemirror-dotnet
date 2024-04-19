using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public class AttrStep : Step {
    public int Pos { get; }
    public string Attr { get; }
    public JsonNode? Value { get; }

    public AttrStep(int pos, string attr, JsonNode? value) {
        Pos = pos;
        Attr = attr;
        Value = value;
    }

    public override StepResult Apply(Node doc) {
        var node = doc.NodeAt(Pos);
        if (node is null) return StepResult.Fail("No node at attribute step's position");
        var attrs = new Attrs(node.Attrs) { [Attr] = Value };
        var updated = node.Type.Create(attrs, null, node.Marks);
        return StepResult.FromReplace(doc, Pos, Pos + 1, new Slice(Fragment.From(updated), 0, node.IsLeaf ? 0 : 1));
    }

    public override Step Invert(Node doc) {
        return new AttrStep(Pos, Attr, doc.NodeAt(Pos)!.Attrs.GetValueOrDefault(Attr));
    }

    public override AttrStep? Map(IMappable mapping) {
        var pos = mapping.MapResult(Pos, 1);
        return pos.DeletedAfter ? null : new AttrStep(pos.Pos, Attr, Value);
    }

    public override AttrStepDto ToJSON() => new() {
        Pos = Pos,
        Attr = Attr,
        Value = Value
    };

    public static AttrStep FromJSON(Schema schema, AttrStepDto json) {
        return new AttrStep(json.Pos, json.Attr, json.Value);
    }
}

public record AttrStepDto : StepDto {
    [JsonIgnore]
    public override string StepType => "attr";
    public int Pos { get; init; }
    public required string Attr { get; init; }
    public JsonNode? Value { get; init; }
}

public class DocAttrStep : Step {
    public string Attr { get; }
    public JsonNode? Value { get; }

    public DocAttrStep(string attr, JsonNode? value) {
        Attr = attr;
        Value = value;
    }

    public override StepResult Apply(Node doc) {
        var attrs = new Attrs(doc.Attrs) { [Attr] = Value };
        var updated = doc.Type.Create(attrs, doc.Content, doc.Marks);
        return StepResult.Ok(updated);
    }

    public override Step Invert(Node doc) {
        return new DocAttrStep(Attr, doc.Attrs.GetValueOrDefault(Attr));
    }

    public override DocAttrStep? Map(IMappable mapping) {
        return this;
    }

    public override DocAttrStepDto ToJSON() => new() {
        Attr = Attr,
        Value = Value
    };

    public static DocAttrStep FromJSON(Schema schema, DocAttrStepDto json) {
        return new DocAttrStep(json.Attr, json.Value);
    }
}

public record DocAttrStepDto : StepDto {
    [JsonIgnore]
    public override string StepType => "docAttr";
    public required string Attr { get; init; }
    public JsonNode? Value { get; init; }
}