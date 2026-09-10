using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Arcade.Events;

public class ArcadeEventWebhookEmbed
{
    [JsonPropertyName("title")]
    public string Title { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("color")]
    public int Color { get; set; }
}
