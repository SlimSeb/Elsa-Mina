using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Keyed by size ("hd", "md", "sm", "xs"), then by format ("gif", "webp", "jpg", "mp4", "webm").
    /// Sponsored items returned inside search results may not carry every size or format.
    /// </summary>
    [JsonPropertyName("file")]
    public Dictionary<string, Dictionary<string, KlipyFile>> File { get; set; }
}
