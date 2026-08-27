using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class InstructionPart
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
