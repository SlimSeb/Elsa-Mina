using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }
}
