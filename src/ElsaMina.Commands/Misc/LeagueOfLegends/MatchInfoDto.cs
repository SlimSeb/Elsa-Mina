using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class MatchInfoDto
{
    [JsonPropertyName("gameDuration")]
    public int GameDuration { get; set; }

    [JsonPropertyName("gameCreation")]
    public long GameCreation { get; set; }

    [JsonPropertyName("gameEndTimestamp")]
    public long GameEndTimestamp { get; set; }

    [JsonPropertyName("queueId")]
    public int QueueId { get; set; }

    [JsonPropertyName("participants")]
    public List<MatchParticipantDto> Participants { get; set; }
}
