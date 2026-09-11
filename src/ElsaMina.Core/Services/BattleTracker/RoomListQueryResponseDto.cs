using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.BattleTracker;

public class RoomListQueryResponseDto
{
    [JsonPropertyName("rooms")]
    public IDictionary<string, ActiveBattleDto> Rooms { get; set; }
}
