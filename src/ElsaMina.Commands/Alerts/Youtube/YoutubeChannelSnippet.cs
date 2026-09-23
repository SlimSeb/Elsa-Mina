using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeChannelSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("customUrl")]
    public string CustomUrl { get; set; }
}
