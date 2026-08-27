using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class ContentPart
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
