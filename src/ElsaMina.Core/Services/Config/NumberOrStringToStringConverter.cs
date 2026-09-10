using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Config;

public sealed class NumberOrStringToStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var intVal))
            {
                return intVal.ToString();
            }
            return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.ToString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
