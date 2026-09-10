using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.RoomInfo;

public class RoomInfoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("roomid")]
    public string RoomId { get; set; }
    [JsonPropertyName("title")]
    public string Title { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; }
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; }
    [JsonPropertyName("modchat")]
    public string Modchat { get; set; }
    [JsonPropertyName("modjoin")]
    public string Modjoin { get; set; }
    [JsonPropertyName("auth")]
    public IDictionary<string, IReadOnlyList<string>> Auth { get; set; }
    [JsonPropertyName("users")]
    public IReadOnlyList<string> Users { get; set; }
    [JsonPropertyName("error")]
    public string Error { get; set; }
}
