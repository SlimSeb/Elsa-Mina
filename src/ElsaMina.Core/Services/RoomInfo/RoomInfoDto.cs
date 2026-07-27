using Newtonsoft.Json;

namespace ElsaMina.Core.Services.RoomInfo;

public class RoomInfoDto
{
    [JsonProperty("id")]
    public string Id { get; set; }
    [JsonProperty("roomid")]
    public string RoomId { get; set; }
    [JsonProperty("title")]
    public string Title { get; set; }
    [JsonProperty("type")]
    public string Type { get; set; }
    [JsonProperty("visibility")]
    public string Visibility { get; set; }
    [JsonProperty("modchat")]
    public string Modchat { get; set; }
    [JsonProperty("modjoin")]
    public string Modjoin { get; set; }
    [JsonProperty("auth")]
    public IDictionary<string, IReadOnlyList<string>> Auth { get; set; }
    [JsonProperty("users")]
    public IReadOnlyList<string> Users { get; set; }
    [JsonProperty("error")]
    public string Error { get; set; }
}
