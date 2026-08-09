using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyItem
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>
    /// Keyed by size ("hd", "md", "sm", "xs"), then by format ("gif", "webp", "jpg", "mp4", "webm").
    /// Sponsored items returned inside search results may not carry every size or format.
    /// </summary>
    [JsonProperty("file")]
    public Dictionary<string, Dictionary<string, KlipyFile>> File { get; set; }
}
