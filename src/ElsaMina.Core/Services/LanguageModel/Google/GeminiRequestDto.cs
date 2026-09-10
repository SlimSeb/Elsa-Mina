using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class GeminiRequestDto
{
    [JsonPropertyName("system_instruction")]
    public SystemInstruction SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; }
}
