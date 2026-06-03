using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class EmbeddingResponseDto
{
    [JsonProperty("data")]
    public List<EmbeddingDataDto> Data { get; set; }
}
