using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchStreamsResponse
{
    [JsonPropertyName("data")]
    public List<TwitchStream> Data { get; set; }
}
