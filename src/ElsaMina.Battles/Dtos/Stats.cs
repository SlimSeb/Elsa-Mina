using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class Stats
{
    [JsonProperty("atk")]
    public int Atk { get; set; }

    [JsonProperty("def")]
    public int Def { get; set; }

    [JsonProperty("spa")]
    public int Spa { get; set; }

    [JsonProperty("spd")]
    public int Spd { get; set; }

    [JsonProperty("spe")]
    public int Spe { get; set; }
}
