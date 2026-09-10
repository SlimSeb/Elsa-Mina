using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class SystemInstruction
{
    [JsonPropertyName("parts")]
    public List<InstructionPart> Parts { get; set; }
}
