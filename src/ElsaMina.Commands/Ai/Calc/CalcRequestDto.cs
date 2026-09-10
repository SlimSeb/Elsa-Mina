using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Structured damage calculation request produced by the language model from a
/// natural language query. Fed to <see cref="IDamageCalculator"/> to compute the result.
/// </summary>
public class CalcRequestDto
{
    [JsonPropertyName("gen")]
    public int Gen { get; set; } = 9;

    [JsonPropertyName("attacker")]
    public CalcPokemonDto Attacker { get; set; }

    [JsonPropertyName("defender")]
    public CalcPokemonDto Defender { get; set; }

    [JsonPropertyName("move")]
    public string Move { get; set; }

    [JsonPropertyName("isCrit")]
    public bool? IsCrit { get; set; }

    [JsonPropertyName("hits")]
    public int? Hits { get; set; }

    [JsonPropertyName("field")]
    public CalcFieldDto Field { get; set; }

    /// <summary>
    /// Set by the language model when the query cannot be interpreted as a damage calculation.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; }
}
