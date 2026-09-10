using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.TextToSpeech;

public class ElevenLabsRequestDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
    
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; }
}