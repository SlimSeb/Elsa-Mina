using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Login;

public class CurrentUserDto
{
    [JsonPropertyName("loggedin")]
    public bool IsLoggedIn { get; set; }
    [JsonPropertyName("userid")]
    public string UserId { get; set; }
    [JsonPropertyName("username")]
    public string Username { get; set; }
}