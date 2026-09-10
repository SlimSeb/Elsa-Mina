using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.UserDetails;

public class UserDetailsDto
{
    [JsonPropertyName("isPrivate")]
    public string Id { get; set; }
    [JsonPropertyName("userid")]
    public string UserId { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; }
    [JsonPropertyName("group")]
    public string Group { get; set; }
    [JsonPropertyName("friended")]
    public bool Friended { get; set; }
    [JsonPropertyName("autoconfirmed")]
    public bool AutoConfirmed { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("rooms")]
    public IDictionary<string, UserDetailsRoomDto> Rooms { get; set; }
}