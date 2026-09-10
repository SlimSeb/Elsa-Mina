using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipySearchData
{
    [JsonPropertyName("data")]
    public List<KlipyItem> Items { get; set; }

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }
}
