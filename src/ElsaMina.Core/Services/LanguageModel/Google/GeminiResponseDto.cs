using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class GeminiResponseDto
{
    [JsonProperty("candidates")]
    public List<Candidate> Candidates { get; set; }

    [JsonProperty("usageMetadata")]
    public UsageMetadata UsageMetadata { get; set; }

    [JsonProperty("modelVersion")]
    public string ModelVersion { get; set; }

    [JsonProperty("responseId")]
    public string ResponseId { get; set; }
}
