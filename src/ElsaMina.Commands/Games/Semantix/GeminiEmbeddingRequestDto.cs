using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingRequestDto
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("content")]
    public GeminiEmbeddingContentDto Content { get; set; }

    [JsonProperty("outputDimensionality")]
    public int OutputDimensionality { get; set; }
}
