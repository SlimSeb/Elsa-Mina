using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipySearchResponse
{
    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("data")]
    public KlipySearchData Data { get; set; }
}
