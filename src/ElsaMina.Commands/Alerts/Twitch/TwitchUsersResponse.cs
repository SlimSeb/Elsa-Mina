using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchUsersResponse
{
    [JsonPropertyName("data")]
    public List<TwitchUser> Data { get; set; }
}
