using System.Text.Json;
using System.Text.Json.Serialization;


namespace StepWise.Prose.Transformation;

public class StepDtoConverter : JsonConverter<StepDto>
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsAssignableTo(typeof(StepDto)); // NOTE: By default (in the parent class's implementation), this only returns true if `typeToConvert` is *equal* to `T`.

    public override StepDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);

        var typeDiscriminator = jsonDoc.RootElement
            .GetProperty("stepType")
            .GetString()!;

        var type = typeDiscriminator switch {
            "replace" => typeof(ReplaceStepDto),
            "replaceAround" => typeof(ReplaceAroundStepDto),
            "addMark" => typeof(AddMarkStepDto),
            "removeMark" => typeof(RemoveMarkStepDto),
            "addNodeMark" => typeof(AddNodeMarkStepDto),
            "removeNodeMark" => typeof(RemoveNodeMarkStepDto),
            "attr" => typeof(AttrStepDto),
            _ => throw new Exception($"No step type {typeDiscriminator} defined")
        };

        return (StepDto?)jsonDoc.Deserialize(type, RemoveThisFromOptions(options));
    }

    public override void Write(Utf8JsonWriter writer, StepDto value, JsonSerializerOptions options)
    {
        var type = value!.GetType();
        writer.WriteStartObject();

        writer.WriteString("stepType", value.StepType);

        using var jsonDoc = JsonSerializer.SerializeToDocument(value, type, RemoveThisFromOptions(options));
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(prop.Name);
            prop.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private JsonSerializerOptions RemoveThisFromOptions(JsonSerializerOptions options)
    {
        JsonSerializerOptions newOptions = new(options);
        newOptions.Converters.Remove(this); // NOTE: We'll get an infinite loop if we don't do this
        return newOptions;
    }
}
