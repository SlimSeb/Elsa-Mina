using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterTweet
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}
