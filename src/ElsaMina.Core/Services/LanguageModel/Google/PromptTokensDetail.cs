using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class PromptTokensDetail
{
    [JsonProperty("modality")]
    public string Modality { get; set; }

    [JsonProperty("tokenCount")]
    public int TokenCount { get; set; }
}
