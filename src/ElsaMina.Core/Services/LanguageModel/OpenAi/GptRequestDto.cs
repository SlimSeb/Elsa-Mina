using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptRequestDto
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("items")]
    public List<GptConversationItemDto> Messages { get; set; }
}
