using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Valorant;

public class RiotAccountDto
{
    [JsonProperty("puuid")]
    public string Puuid { get; set; }

    [JsonProperty("gameName")]
    public string GameName { get; set; }

    [JsonProperty("tagLine")]
    public string TagLine { get; set; }
}
