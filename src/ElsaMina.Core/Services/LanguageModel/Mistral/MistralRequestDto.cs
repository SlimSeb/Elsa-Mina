using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralRequestDto
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public List<MistralRequestMessageDto> Messages { get; set; }
}
