using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

[JsonConverter(typeof(DictionaryApiResponseConverter))]
public class DictionaryApiResponse
{
    public List<DictionaryApiEntry> Entries { get; init; }
    public List<string> Suggestions { get; init; }

    public bool HasSuggestions => Suggestions is { Count: > 0 };
    public bool IsEmpty => (Entries == null || Entries.Count == 0) && !HasSuggestions;
}

public class DictionaryApiResponseConverter : JsonConverter<DictionaryApiResponse>
{
    public override DictionaryApiResponse Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var token = JsonDocument.ParseValue(ref reader);
        var root = token.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return new DictionaryApiResponse();
        }

        if (root[0].ValueKind == JsonValueKind.String)
        {
            return new DictionaryApiResponse { Suggestions = root.Deserialize<List<string>>(options) };
        }

        return new DictionaryApiResponse { Entries = root.Deserialize<List<DictionaryApiEntry>>(options) };
    }

    public override void Write(Utf8JsonWriter writer, DictionaryApiResponse value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
