using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Showdown.Dex;

/// <summary>
/// Move accuracy is either a percentage or <c>true</c> for moves that never miss, which is mapped to null.
/// </summary>
public class MoveAccuracyJsonConverter : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.True or JsonTokenType.Null => null,
            JsonTokenType.False => 0,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for move accuracy")
        };
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteBooleanValue(true);
        }
    }
}
