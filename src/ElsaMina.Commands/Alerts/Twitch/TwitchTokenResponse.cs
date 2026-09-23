using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
