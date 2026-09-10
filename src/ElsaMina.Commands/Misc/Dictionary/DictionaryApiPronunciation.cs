using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiPronunciation
{
    [JsonPropertyName("mw")]
    public string MerriamWebster { get; set; }

    [JsonPropertyName("sound")]
    public DictionaryApiSound Sound { get; set; }
}
