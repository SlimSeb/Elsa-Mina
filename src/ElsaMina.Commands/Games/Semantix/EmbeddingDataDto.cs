using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class EmbeddingDataDto
{
    [JsonProperty("embedding")]
    public float[] Embedding { get; set; }
}
