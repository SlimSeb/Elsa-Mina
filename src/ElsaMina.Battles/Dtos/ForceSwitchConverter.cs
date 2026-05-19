#nullable enable
using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class ForceSwitchConverter : JsonConverter<List<bool>>
{
    public override List<bool> ReadJson(
        JsonReader reader,
        Type objectType,
        List<bool>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Boolean)
        {
            return (bool)reader.Value! ? [true] : [];
        }

        if (reader.TokenType == JsonToken.StartArray)
        {
            return serializer.Deserialize<List<bool>>(reader) ?? [];
        }

        return [];
    }

    public override void WriteJson(JsonWriter writer, List<bool>? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value ?? []);
    }
}
