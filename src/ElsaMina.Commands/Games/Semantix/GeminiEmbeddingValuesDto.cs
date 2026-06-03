using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingValuesDto
{
    [JsonProperty("values")]
    public float[] Values { get; set; }
}
