using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class LeagueEntryDto
{
    [JsonPropertyName("queueType")]
    public string QueueType { get; set; }

    [JsonPropertyName("tier")]
    public string Tier { get; set; }

    [JsonPropertyName("rank")]
    public string Rank { get; set; }

    [JsonPropertyName("leaguePoints")]
    public int LeaguePoints { get; set; }

    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    [JsonPropertyName("losses")]
    public int Losses { get; set; }
}
