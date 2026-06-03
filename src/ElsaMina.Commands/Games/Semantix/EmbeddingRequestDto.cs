using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.Semantix;

public class EmbeddingRequestDto
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("input")]
    public string Input { get; set; }

    [JsonProperty("dimensions")]
    public int Dimensions { get; set; }
}
