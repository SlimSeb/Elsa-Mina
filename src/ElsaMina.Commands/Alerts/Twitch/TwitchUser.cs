using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }
}
