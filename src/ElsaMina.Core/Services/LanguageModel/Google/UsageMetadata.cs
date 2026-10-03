using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class UsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }

    [JsonPropertyName("promptTokensDetails")]
    public List<PromptTokensDetail> PromptTokensDetails { get; set; }

    [JsonPropertyName("thoughtsTokenCount")]
    public int ThoughtsTokenCount { get; set; }
}
