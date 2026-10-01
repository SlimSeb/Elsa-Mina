using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class SystemInstruction
{
    [JsonPropertyName("parts")]
    public List<InstructionPart> Parts { get; set; }
}
