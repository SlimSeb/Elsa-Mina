#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
            var typeInfo = (JsonTypeInfo<Dictionary<string, UserDetailsRoomDto>>?)options?.GetTypeInfo(typeof(Dictionary<string, UserDetailsRoomDto>))
                ?? ElsaMinaJsonContext.Default.DictionaryStringUserDetailsRoomDto;
            return JsonSerializer.Deserialize(ref reader, typeInfo);
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

        var typeInfo = (JsonTypeInfo<IDictionary<string, UserDetailsRoomDto>>?)options?.GetTypeInfo(typeof(IDictionary<string, UserDetailsRoomDto>))
            ?? ElsaMinaJsonContext.Default.IDictionaryStringUserDetailsRoomDto;
        JsonSerializer.Serialize(writer, value, typeInfo);
    }
}
