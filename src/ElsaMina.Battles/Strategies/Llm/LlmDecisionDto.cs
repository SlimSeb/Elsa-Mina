using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.Llm;

public class LlmDecisionDto
{
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; }

    [JsonPropertyName("decision")]
    public string Decision { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("terastallize")]
    public bool Terastallize { get; set; }
}
