using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeVideoSnippet
{
    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; }

    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// "live", "upcoming" or "none".
    /// </summary>
    [JsonPropertyName("liveBroadcastContent")]
    public string LiveBroadcastContent { get; set; }

    [JsonPropertyName("thumbnails")]
    public YoutubeThumbnails Thumbnails { get; set; }
}
