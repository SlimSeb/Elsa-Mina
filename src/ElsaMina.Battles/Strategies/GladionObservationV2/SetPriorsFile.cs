using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.GladionObservationV2;

/// <summary>
/// set_priors.json, exported with an encoder v2 model: the roles of every species with random battle sets.
/// </summary>
public sealed class SetPriorsFile
{
    /// <summary>
    /// Roles by species id, in the sets file's order.
    /// </summary>
    [JsonPropertyName("species")]
    public Dictionary<string, List<SetPriorsRole>> Species { get; set; } = [];
}
