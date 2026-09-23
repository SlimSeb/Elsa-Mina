using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeVideo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("snippet")]
    public YoutubeVideoSnippet Snippet { get; set; }

    [JsonPropertyName("liveStreamingDetails")]
    public YoutubeLiveStreamingDetails LiveStreamingDetails { get; set; }
}
