using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Youtube;

public class YouTubeVideoListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideoItem> Items { get; set; }
}
