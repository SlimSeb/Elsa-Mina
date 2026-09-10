using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.UserData;

public class UserDataDto
{
    [JsonPropertyName("username")]
    public string UserName { get; set; }
    [JsonPropertyName("userid")]
    public string UserId { get; set; }
    [JsonPropertyName("registertime")]
    public long RegisterTime { get; set; }
    [JsonPropertyName("group")]
    public long Group { get; set; }
    [JsonPropertyName("ratings")]
    public IDictionary<string, UserDataRankingDto> Ratings { get; set; }
}