using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class MatchInfoDto
{
    [JsonProperty("gameDuration")]
    public int GameDuration { get; set; }

    [JsonProperty("gameCreation")]
    public long GameCreation { get; set; }

    [JsonProperty("gameEndTimestamp")]
    public long GameEndTimestamp { get; set; }

    [JsonProperty("queueId")]
    public int QueueId { get; set; }

    [JsonProperty("participants")]
    public List<MatchParticipantDto> Participants { get; set; }
}
