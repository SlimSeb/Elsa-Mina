using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

/// <summary>
/// Reads move data fields that are either <c>true</c> or a string (e.g. <c>selfSwitch: "copyvolatile"</c>,
/// <c>isMax: "Pikachu"</c>) as a boolean: true for <c>true</c>, a non-empty string or a non-zero number.
/// </summary>
public class TruthyJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False or JsonTokenType.Null:
                return false;
            case JsonTokenType.String:
                return !string.IsNullOrEmpty(reader.GetString());
            case JsonTokenType.Number:
                return reader.GetDouble() != 0;
            default:
                reader.Skip();
                return true;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
