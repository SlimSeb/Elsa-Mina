using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Google;

public class InstructionPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}
