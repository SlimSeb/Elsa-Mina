using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.OpenAi;

public class GptRequestDto
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("items")]
    public List<GptConversationItemDto> Messages { get; set; }
}
