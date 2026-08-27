using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptConversationItemDto
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }
}
