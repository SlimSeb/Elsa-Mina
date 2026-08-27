using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralResponseDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("choices")]
    public List<MistralChoiceDto> Choices { get; set; }
}
