using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class CandidateContent
{
    [JsonProperty("parts")]
    public List<CandidatePart> Parts { get; set; }

    [JsonProperty("role")]
    public string Role { get; set; }
}
