using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingContentDto
{
    [JsonProperty("parts")]
    public List<GeminiEmbeddingPartDto> Parts { get; set; }
}
