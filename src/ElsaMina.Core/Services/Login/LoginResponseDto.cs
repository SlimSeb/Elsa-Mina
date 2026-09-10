using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Login;

public class LoginResponseDto
{
    [JsonPropertyName("actionsuccess")]
    public bool IsSuccess { get; set; }
    [JsonPropertyName("assertion")]
    public string Assertion { get; set; }
    [JsonPropertyName("curuser")]
    public CurrentUserDto CurrentUser { get; set; }
}