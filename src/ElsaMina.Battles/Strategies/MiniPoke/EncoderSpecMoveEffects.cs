using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// The "move_effects" section of a minipoke-v2 spec (effects.py): every rule that decides whether a move can work
/// on its target, as data.
/// </summary>
public sealed class EncoderSpecMoveEffects
{
    [JsonPropertyName("foe_targets")]
    public List<string> FoeTargets { get; set; } = [];

    [JsonPropertyName("damaging")]
    public List<string> Damaging { get; set; } = [];

    /// <summary>
    /// The abilities that make their holder immune to a move, by ability id.
    /// </summary>
    [JsonPropertyName("ability_rules")]
    public Dictionary<string, EncoderSpecMoveEffectRule> AbilityRules { get; set; } = [];

    /// <summary>
    /// The items that make their holder immune to a move, by item id.
    /// </summary>
    [JsonPropertyName("item_rules")]
    public Dictionary<string, EncoderSpecMoveEffectRule> ItemRules { get; set; } = [];

    /// <summary>
    /// Immunities of the target's types (status immunities, powder, Leech Seed).
    /// </summary>
    [JsonPropertyName("type_rules")]
    public List<EncoderSpecMoveEffectRule> TypeRules { get; set; } = [];

    /// <summary>
    /// The owner abilities that ignore the target's ability (Mold Breaker...).
    /// </summary>
    [JsonPropertyName("breakers")]
    public List<string> Breakers { get; set; } = [];

    [JsonPropertyName("ignore_ability_moves")]
    public List<string> IgnoreAbilityMoves { get; set; } = [];

    /// <summary>
    /// The status moves that follow the type chart (Thunder Wave).
    /// </summary>
    [JsonPropertyName("type_chart_status_moves")]
    public List<string> TypeChartStatusMoves { get; set; } = [];

    [JsonPropertyName("status_fail_except")]
    public List<string> StatusFailExcept { get; set; } = [];

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];
}
