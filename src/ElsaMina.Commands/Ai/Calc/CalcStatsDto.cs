using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// A set of per-stat integer values, reused for EVs, IVs and stat boosts.
/// </summary>
public class CalcStatsDto
{
    [JsonPropertyName("hp")]
    public int? Hp { get; set; }

    [JsonPropertyName("atk")]
    public int? Atk { get; set; }

    [JsonPropertyName("def")]
    public int? Def { get; set; }

    [JsonPropertyName("spa")]
    public int? Spa { get; set; }

    [JsonPropertyName("spd")]
    public int? Spd { get; set; }

    [JsonPropertyName("spe")]
    public int? Spe { get; set; }
}
