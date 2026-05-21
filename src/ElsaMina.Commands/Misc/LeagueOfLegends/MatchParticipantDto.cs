using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class MatchParticipantDto
{
    [JsonProperty("puuid")]
    public string Puuid { get; set; }

    [JsonProperty("championName")]
    public string ChampionName { get; set; }

    [JsonProperty("kills")]
    public int Kills { get; set; }

    [JsonProperty("deaths")]
    public int Deaths { get; set; }

    [JsonProperty("assists")]
    public int Assists { get; set; }

    [JsonProperty("win")]
    public bool Win { get; set; }

    [JsonProperty("totalMinionsKilled")]
    public int TotalMinionsKilled { get; set; }

    [JsonProperty("neutralMinionsKilled")]
    public int NeutralMinionsKilled { get; set; }
}
