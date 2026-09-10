using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiEntry
{
    [JsonPropertyName("meta")]
    public DictionaryApiMeta Meta { get; set; }

    [JsonPropertyName("hwi")]
    public DictionaryApiHeadwordInfo HeadwordInfo { get; set; }

    [JsonPropertyName("fl")]
    public string PartOfSpeech { get; set; }

    [JsonPropertyName("def")]
    public List<DictionaryApiDefinition> Definitions { get; set; }

    [JsonPropertyName("uros")]
    public List<DictionaryApiUndefinedRunOn> UndefinedRunOns { get; set; }

    [JsonPropertyName("shortdef")]
    public List<string> ShortDefinitions { get; set; }
}
