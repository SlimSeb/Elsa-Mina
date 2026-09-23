using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeVideosResponse
{
    [JsonPropertyName("items")]
    public List<YoutubeVideo> Items { get; set; }
}
