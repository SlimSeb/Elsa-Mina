using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
