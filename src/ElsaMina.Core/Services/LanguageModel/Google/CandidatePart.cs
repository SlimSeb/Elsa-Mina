using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class CandidatePart
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
