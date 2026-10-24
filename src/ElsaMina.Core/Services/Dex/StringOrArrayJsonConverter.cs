using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

/// <summary>
/// Reads pokedex fields that are either one name or a list of names (e.g. <c>battleOnly: "Palafin"</c>,
/// <c>battleOnly: ["Necrozma-Dawn-Wings", "Necrozma-Dusk-Mane"]</c>) as an array.
/// </summary>
public class StringOrArrayJsonConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => [reader.GetString()],
            JsonTokenType.StartArray => JsonSerializer.Deserialize<string[]>(ref reader, options),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for a name or a list of names")
        };
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
