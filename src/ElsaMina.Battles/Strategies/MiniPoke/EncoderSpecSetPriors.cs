using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// The random battle set priors a minipoke-v2 model was trained with, exported next to it.
/// </summary>
public sealed class EncoderSpecSetPriors
{
    /// <summary>
    /// The priors' file name in the model directory (set_priors.json).
    /// </summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    /// <summary>
    /// SHA-256 of the file's bytes.
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    /// <summary>
    /// How many abilities and items are kept per Pokémon (A).
    /// </summary>
    [JsonPropertyName("abilities_items_a")]
    public int AbilitiesAndItems { get; set; }
}
