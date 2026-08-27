using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptResponseDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("items")]
    public List<GptConversationItemDto> Items { get; set; }
}
