#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.UserDetails;

public sealed class UserDetailsRoomsConverter : JsonConverter<IDictionary<string, UserDetailsRoomDto>>
{
    public override IDictionary<string, UserDetailsRoomDto>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.False or JsonTokenType.True or JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<Dictionary<string, UserDetailsRoomDto>>(ref reader, options);
        }

        reader.Skip();
        return null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IDictionary<string, UserDetailsRoomDto>? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}
