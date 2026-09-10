using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiSound
{
    [JsonPropertyName("audio")]
    public string Audio { get; set; }
}
