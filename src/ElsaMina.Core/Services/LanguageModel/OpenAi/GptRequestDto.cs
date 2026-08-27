using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptRequestDto
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("items")]
    public List<GptConversationItemDto> Messages { get; set; }
}
