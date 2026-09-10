using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Youtube;

public class YouTubeVideoItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("snippet")]
    public Snippet Snippet { get; set; }
}
