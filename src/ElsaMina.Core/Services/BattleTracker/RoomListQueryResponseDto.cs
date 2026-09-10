using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.BattleTracker;

internal class RoomListQueryResponseDto
{
    [JsonPropertyName("rooms")]
    public IDictionary<string, ActiveBattleDto> Rooms { get; set; }
}
