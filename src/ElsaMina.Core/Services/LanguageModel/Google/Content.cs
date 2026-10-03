using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class Content
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("parts")]
    public List<ContentPart> Parts { get; set; }
}
