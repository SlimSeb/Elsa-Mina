using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeLiveStreamingDetails
{
    [JsonPropertyName("actualStartTime")]
    public DateTimeOffset? ActualStartTime { get; set; }
}
