using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class ContentPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}
