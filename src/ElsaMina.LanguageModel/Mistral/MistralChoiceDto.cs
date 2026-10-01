using System.Text.Json.Serialization;

namespace ElsaMina.LanguageModel.Mistral;

public class MistralChoiceDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MistralResponseMessageDto Message { get; set; }
}
