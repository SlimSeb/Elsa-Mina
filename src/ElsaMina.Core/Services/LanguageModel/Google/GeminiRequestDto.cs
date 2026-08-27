using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class GeminiRequestDto
{
    [JsonProperty("system_instruction")]
    public SystemInstruction SystemInstruction { get; set; }

    [JsonProperty("contents")]
    public List<Content> Contents { get; set; }
}
