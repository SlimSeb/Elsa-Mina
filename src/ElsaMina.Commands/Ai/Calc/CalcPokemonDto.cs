using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Describes one participant (attacker or defender) of a damage calculation.
/// </summary>
public class CalcPokemonDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("nature")]
    public string Nature { get; set; }

    [JsonPropertyName("ability")]
    public string Ability { get; set; }

    [JsonPropertyName("item")]
    public string Item { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("teraType")]
    public string TeraType { get; set; }

    [JsonPropertyName("evs")]
    public CalcStatsDto Evs { get; set; }

    [JsonPropertyName("ivs")]
    public CalcStatsDto Ivs { get; set; }

    [JsonPropertyName("boosts")]
    public CalcStatsDto Boosts { get; set; }
}
