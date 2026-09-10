using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiMeta
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; }

    [JsonPropertyName("src")]
    public string Source { get; set; }

    [JsonPropertyName("section")]
    public string Section { get; set; }

    [JsonPropertyName("stems")]
    public List<string> Stems { get; set; }

    [JsonPropertyName("offensive")]
    public bool IsOffensive { get; set; }
}
