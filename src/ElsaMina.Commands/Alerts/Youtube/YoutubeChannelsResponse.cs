using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeChannelsResponse
{
    [JsonPropertyName("items")]
    public List<YoutubeChannel> Items { get; set; }
}
