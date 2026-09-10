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
            return JsonSerializer.Deserialize<List<bool>>(ref reader, options) ?? [];
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<bool>? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value ?? [], options);
    }
}
