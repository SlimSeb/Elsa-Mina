using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.UserData;

public class UserDataRankingDto
{
    [JsonPropertyName("elo")]
    public double Elo { get; set; }
    [JsonPropertyName("gxe")]
    public double Gxe { get; set; }
    [JsonPropertyName("rpr")]
    public double Rpr { get; set; }
    [JsonPropertyName("rprd")]
    public double Rprd { get; set; }
}