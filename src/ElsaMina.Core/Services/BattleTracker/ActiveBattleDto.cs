using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.BattleTracker;

public class ActiveBattleDto
{
    public string RoomId { get; set; }

    [JsonPropertyName("p1")]
    public string Player1 { get; set; }

    [JsonPropertyName("p2")]
    public string Player2 { get; set; }

    [JsonPropertyName("minElo")]
    public int? MinElo { get; set; }
}
