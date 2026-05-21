using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class SummonerDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("puuid")]
    public string Puuid { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("profileIconId")]
    public int ProfileIconId { get; set; }

    [JsonProperty("summonerLevel")]
    public long SummonerLevel { get; set; }
}
