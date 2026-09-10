using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class SummonerDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("puuid")]
    public string Puuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("profileIconId")]
    public int ProfileIconId { get; set; }

    [JsonPropertyName("summonerLevel")]
    public long SummonerLevel { get; set; }
}
