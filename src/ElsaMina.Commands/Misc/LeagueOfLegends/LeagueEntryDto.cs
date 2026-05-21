using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class LeagueEntryDto
{
    [JsonProperty("queueType")]
    public string QueueType { get; set; }

    [JsonProperty("tier")]
    public string Tier { get; set; }

    [JsonProperty("rank")]
    public string Rank { get; set; }

    [JsonProperty("leaguePoints")]
    public int LeaguePoints { get; set; }

    [JsonProperty("wins")]
    public int Wins { get; set; }

    [JsonProperty("losses")]
    public int Losses { get; set; }
}
