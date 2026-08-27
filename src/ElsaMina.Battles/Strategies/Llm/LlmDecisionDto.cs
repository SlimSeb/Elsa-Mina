using Newtonsoft.Json;

namespace ElsaMina.Battles.Strategies.Llm;

public class LlmDecisionDto
{
    [JsonProperty("reasoning")]
    public string Reasoning { get; set; }

    [JsonProperty("decision")]
    public string Decision { get; set; }

    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("terastallize")]
    public bool Terastallize { get; set; }
}
