using System.Text.Json;
using System.Text.Json.Serialization;

using StepWise.Prose.Model;
using StepWise.Prose.Text.Json;


namespace StepWise.Prose.Transformation;

public abstract class Step {
    public abstract StepResult Apply(Node doc);

    public virtual StepMap GetMap() => StepMap.Empty;

    public abstract Step Invert(Node doc);

    public abstract Step? Map(IMappable mapping);

    public virtual Step? Merge(Step other) => null;

    public abstract StepDto ToJSON();

    public static Step FromJSON(Schema schema, StepDto json) {
        return json switch {
            ReplaceStepDto dto => ReplaceStep.FromJSON(schema, dto),
            ReplaceAroundStepDto dto => ReplaceAroundStep.FromJSON(schema, dto),
            AddMarkStepDto dto => AddMarkStep.FromJSON(schema, dto),
            RemoveMarkStepDto dto => RemoveMarkStep.FromJSON(schema, dto),
            AddNodeMarkStepDto dto => AddNodeMarkStep.FromJSON(schema, dto),
            RemoveNodeMarkStepDto dto => RemoveNodeMarkStep.FromJSON(schema, dto),
            AttrStepDto dto => AttrStep.FromJSON(schema, dto),
            _ => throw new Exception($"No step type {json.StepType} defined")
        };
    }
}

public class StepResult {
    public Node? Doc { get; }
    public string? Failed { get; }

    public StepResult(Node? doc, string? failed) {
        Doc = doc;
        Failed = failed;
    }

    public static StepResult Ok(Node doc) => new(doc, null);

    public static StepResult Fail(string message) => new(null, message);

    public static StepResult FromReplace(Node doc, int from, int to, Slice slice) {
        try {
            return Ok(doc.Replace(from, to, slice));
        } catch (ReplaceException e) {
            return Fail(e.Message);
        }
    }
}

[JsonConverter(typeof(StepDtoConverter))]
public abstract record StepDto {
    [JsonIgnore]
    public abstract string StepType { get; }

    public string ToJson() => JsonSerializer.Serialize(this, ProseJson.JsonOptions);
    public static StepDto? FromJson(string json) => JsonSerializer.Deserialize<StepDto>(json, ProseJson.JsonOptions);
    public static StepDto? FromJson(JsonElement json) => JsonSerializer.Deserialize<StepDto>(json, ProseJson.JsonOptions);
}
