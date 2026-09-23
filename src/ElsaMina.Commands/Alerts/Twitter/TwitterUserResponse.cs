using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterUserResponse
{
    [JsonPropertyName("data")]
    public TwitterUser Data { get; set; }
}
