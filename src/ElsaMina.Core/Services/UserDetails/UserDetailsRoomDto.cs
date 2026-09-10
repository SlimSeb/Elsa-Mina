using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.UserDetails;

public class UserDetailsRoomDto
{
    [JsonPropertyName("isPrivate")]
    public bool IsPrivate { get; set; }
}