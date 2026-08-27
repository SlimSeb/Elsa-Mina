using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class UsageMetadata
{
    [JsonProperty("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonProperty("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonProperty("totalTokenCount")]
    public int TotalTokenCount { get; set; }

    [JsonProperty("promptTokensDetails")]
    public List<PromptTokensDetail> PromptTokensDetails { get; set; }

    [JsonProperty("thoughtsTokenCount")]
    public int ThoughtsTokenCount { get; set; }
}
