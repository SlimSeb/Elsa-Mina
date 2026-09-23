using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterTweetsMeta
{
    [JsonPropertyName("newest_id")]
    public string NewestId { get; set; }
}
