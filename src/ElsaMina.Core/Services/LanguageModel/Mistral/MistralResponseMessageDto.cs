using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralResponseMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; }
}
