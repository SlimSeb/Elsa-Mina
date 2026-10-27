using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// One rule of effects.py: when a move has no effect on its target. It matches when every condition it has holds;
/// a missing one (null) always holds. Ids are lower case: move types ("ground"), categories ("status"), target
/// types, statuses ("brn"), abilities and move ids.
/// </summary>
public sealed class EncoderSpecMoveEffectRule
{
    /// <summary>
    /// The move's type is one of them.
    /// </summary>
    [JsonPropertyName("types")]
    public List<string> Types { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; }

    /// <summary>
    /// The move has one of these data flags (sound, bullet, powder...).
    /// </summary>
    [JsonPropertyName("flags")]
    public List<string> Flags { get; set; }

    /// <summary>
    /// The major status the move inflicts (its data "status") is one of them.
    /// </summary>
    [JsonPropertyName("statuses")]
    public List<string> Statuses { get; set; }

    [JsonPropertyName("moves")]
    public List<string> Moves { get; set; }

    [JsonPropertyName("except")]
    public List<string> Except { get; set; }

    /// <summary>
    /// The move's data target is one of them; null means the spec's foe targets.
    /// </summary>
    [JsonPropertyName("targets")]
    public List<string> Targets { get; set; }

    /// <summary>
    /// The target has one of these types.
    /// </summary>
    [JsonPropertyName("target_types")]
    public List<string> TargetTypes { get; set; }

    [JsonPropertyName("min_priority")]
    public int? MinPriority { get; set; }

    /// <summary>
    /// A damaging move that is not super effective (Wonder Guard).
    /// </summary>
    [JsonPropertyName("not_super_effective")]
    public bool NotSuperEffective { get; set; }

    /// <summary>
    /// The owner's known ability is none of these.
    /// </summary>
    [JsonPropertyName("unless_owner")]
    public List<string> UnlessOwner { get; set; }
}
