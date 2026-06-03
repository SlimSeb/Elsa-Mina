using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Structured damage calculation request produced by the language model from a
/// natural language query. Fed to <see cref="IDamageCalculator"/> to compute the result.
/// </summary>
public class CalcRequestDto
{
    [JsonProperty("gen")]
    public int Gen { get; set; } = 9;

    [JsonProperty("attacker")]
    public CalcPokemonDto Attacker { get; set; }

    [JsonProperty("defender")]
    public CalcPokemonDto Defender { get; set; }

    [JsonProperty("move")]
    public string Move { get; set; }

    [JsonProperty("isCrit")]
    public bool? IsCrit { get; set; }

    [JsonProperty("hits")]
    public int? Hits { get; set; }

    [JsonProperty("field")]
    public CalcFieldDto Field { get; set; }

    /// <summary>
    /// Set by the language model when the query cannot be interpreted as a damage calculation.
    /// </summary>
    [JsonProperty("error")]
    public string Error { get; set; }
}
