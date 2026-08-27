using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class SystemInstruction
{
    [JsonProperty("parts")]
    public List<InstructionPart> Parts { get; set; }
}
