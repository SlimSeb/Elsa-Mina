using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class Content
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("parts")]
    public List<ContentPart> Parts { get; set; }
}
