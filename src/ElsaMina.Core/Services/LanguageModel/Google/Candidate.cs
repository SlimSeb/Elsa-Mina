using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class Candidate
{
    [JsonProperty("content")]
    public CandidateContent Content { get; set; }

    [JsonProperty("finishReason")]
    public string FinishReason { get; set; }

    [JsonProperty("index")]
    public int Index { get; set; }
}
