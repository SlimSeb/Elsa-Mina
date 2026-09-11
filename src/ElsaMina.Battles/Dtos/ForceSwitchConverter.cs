#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class ForceSwitchConverter : JsonConverter<List<bool>>
{
    public override List<bool> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True)
        {
            return [true];
        }

        if (reader.TokenType == JsonTokenType.False)
        {
            return [];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var typeInfo = (options?.GetTypeInfo(typeof(List<bool>)) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<bool>>)
                ?? ElsaMinaBattlesJsonContext.Default.ListBoolean;
            return JsonSerializer.Deserialize(ref reader, typeInfo) ?? [];
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<bool>? value, JsonSerializerOptions options)
    {
        var typeInfo = (options?.GetTypeInfo(typeof(List<bool>)) as System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<bool>>)
            ?? ElsaMinaBattlesJsonContext.Default.ListBoolean;
        JsonSerializer.Serialize(writer, value ?? [], typeInfo);
    }
}
