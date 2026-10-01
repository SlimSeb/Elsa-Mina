using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class Candidate
{
    [JsonPropertyName("content")]
    public CandidateContent Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}
