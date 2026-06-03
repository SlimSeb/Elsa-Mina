using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// A set of per-stat integer values, reused for EVs, IVs and stat boosts.
/// </summary>
public class CalcStatsDto
{
    [JsonProperty("hp")]
    public int? Hp { get; set; }

    [JsonProperty("atk")]
    public int? Atk { get; set; }

    [JsonProperty("def")]
    public int? Def { get; set; }

    [JsonProperty("spa")]
    public int? Spa { get; set; }

    [JsonProperty("spd")]
    public int? Spd { get; set; }

    [JsonProperty("spe")]
    public int? Spe { get; set; }
}
