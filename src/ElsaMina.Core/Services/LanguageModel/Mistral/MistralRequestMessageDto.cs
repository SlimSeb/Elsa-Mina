using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralRequestMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}
