using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Arcade.Events;

public class ArcadeEventWebhookBody
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }
    [JsonPropertyName("content")]
    public string Content { get; set; }
    [JsonPropertyName("embeds")]
    public List<ArcadeEventWebhookEmbed> Embeds { get; set; }
}