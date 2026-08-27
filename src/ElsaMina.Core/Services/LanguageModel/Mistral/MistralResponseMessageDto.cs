using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralResponseMessageDto
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("finish_reason")]
    public string FinishReason { get; set; }
}
