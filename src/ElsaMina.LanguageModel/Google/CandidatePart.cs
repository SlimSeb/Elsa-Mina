using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class CandidatePart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}
