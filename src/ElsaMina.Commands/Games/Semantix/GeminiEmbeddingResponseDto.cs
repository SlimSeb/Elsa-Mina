using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingResponseDto
{
    [JsonProperty("embedding")]
    public GeminiEmbeddingValuesDto Embedding { get; set; }
}
