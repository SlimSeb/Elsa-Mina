using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class RiotAccountDto
{
    [JsonPropertyName("puuid")]
    public string Puuid { get; set; }

    [JsonPropertyName("gameName")]
    public string GameName { get; set; }

    [JsonPropertyName("tagLine")]
    public string TagLine { get; set; }
}
