using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Describes one participant (attacker or defender) of a damage calculation.
/// </summary>
public class CalcPokemonDto
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("level")]
    public int? Level { get; set; }

    [JsonProperty("nature")]
    public string Nature { get; set; }

    [JsonProperty("ability")]
    public string Ability { get; set; }

    [JsonProperty("item")]
    public string Item { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("teraType")]
    public string TeraType { get; set; }

    [JsonProperty("evs")]
    public CalcStatsDto Evs { get; set; }

    [JsonProperty("ivs")]
    public CalcStatsDto Ivs { get; set; }

    [JsonProperty("boosts")]
    public CalcStatsDto Boosts { get; set; }
}
