using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiHeadwordInfo
{
    [JsonPropertyName("hw")]
    public string Headword { get; set; }

    [JsonPropertyName("prs")]
    public List<DictionaryApiPronunciation> Pronunciations { get; set; }
}
