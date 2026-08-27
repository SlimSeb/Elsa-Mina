using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralRequestDto
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public List<MistralRequestMessageDto> Messages { get; set; }
}
