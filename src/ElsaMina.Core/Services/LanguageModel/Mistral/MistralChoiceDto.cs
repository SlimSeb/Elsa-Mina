using Newtonsoft.Json;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralChoiceDto
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("message")]
    public MistralResponseMessageDto Message { get; set; }
}
