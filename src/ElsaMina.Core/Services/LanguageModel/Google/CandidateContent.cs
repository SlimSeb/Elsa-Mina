using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class CandidateContent
{
    [JsonPropertyName("parts")]
    public List<CandidatePart> Parts { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }
}
