using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiUndefinedRunOn
{
    [JsonPropertyName("ure")]
    public string UndefinedRunOn { get; set; }

    [JsonPropertyName("prs")]
    public List<DictionaryApiPronunciation> Pronunciations { get; set; }

    [JsonPropertyName("fl")]
    public string PartOfSpeech { get; set; }
}
