using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralResponseDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("choices")]
    public List<MistralChoiceDto> Choices { get; set; }
}
