using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// One role of a species in Showdown's random battle sets ("Fast Attacker", "Bulky Support"...), as
/// set_priors.json holds it: ids, sorted.
/// </summary>
public sealed class SetPriorsRole
{
    [JsonPropertyName("role")]
    public string Name { get; set; } = "";

    [JsonPropertyName("moves")]
    public List<string> Moves { get; set; } = [];

    [JsonPropertyName("abilities")]
    public List<string> Abilities { get; set; } = [];

    /// <summary>
    /// Empty with the gen 9 sets: Showdown picks items in code.
    /// </summary>
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = [];

    [JsonPropertyName("tera_types")]
    public List<string> TeraTypes { get; set; } = [];
}
