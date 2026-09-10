using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class InstructionPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}
