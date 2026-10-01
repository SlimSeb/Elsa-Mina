using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class CandidateContent
{
    [JsonPropertyName("parts")]
    public List<CandidatePart> Parts { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }
}
