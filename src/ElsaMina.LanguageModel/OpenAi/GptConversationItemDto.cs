using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.OpenAi;

public class GptConversationItemDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}
