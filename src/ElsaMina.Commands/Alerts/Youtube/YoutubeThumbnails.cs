using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeThumbnails
{
    [JsonPropertyName("default")]
    public YoutubeThumbnail Default { get; set; }

    [JsonPropertyName("medium")]
    public YoutubeThumbnail Medium { get; set; }
}
