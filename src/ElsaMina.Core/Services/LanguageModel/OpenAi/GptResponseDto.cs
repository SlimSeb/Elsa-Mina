using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptResponseDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("items")]
    public List<GptConversationItemDto> Items { get; set; }
}
