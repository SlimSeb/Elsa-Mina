using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeChannel
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("snippet")]
    public YoutubeChannelSnippet Snippet { get; set; }
}
