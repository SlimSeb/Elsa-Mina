using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingPartDto
{
    [JsonProperty("text")]
    public string Text { get; set; }
}
