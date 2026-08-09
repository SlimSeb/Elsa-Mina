using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipySearchData
{
    [JsonProperty("data")]
    public List<KlipyItem> Items { get; set; }

    [JsonProperty("current_page")]
    public int CurrentPage { get; set; }

    [JsonProperty("per_page")]
    public int PerPage { get; set; }

    [JsonProperty("has_next")]
    public bool HasNext { get; set; }
}
