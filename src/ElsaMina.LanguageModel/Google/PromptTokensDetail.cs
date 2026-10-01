using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class PromptTokensDetail
{
    [JsonPropertyName("modality")]
    public string Modality { get; set; }

    [JsonPropertyName("tokenCount")]
    public int TokenCount { get; set; }
}
