using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipySearchResponse
{
    [JsonProperty("result")]
    public bool Result { get; set; }

    [JsonProperty("data")]
    public KlipySearchData Data { get; set; }
}
