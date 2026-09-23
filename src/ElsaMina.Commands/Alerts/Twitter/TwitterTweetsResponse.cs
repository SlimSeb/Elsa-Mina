using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterTweetsResponse
{
    [JsonPropertyName("data")]
    public List<TwitterTweet> Data { get; set; }

    [JsonPropertyName("meta")]
    public TwitterTweetsMeta Meta { get; set; }
}
