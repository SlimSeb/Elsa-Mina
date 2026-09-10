using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Field conditions affecting one side of the battle (the attacker's or the defender's).
/// </summary>
public class CalcSideDto
{
    [JsonPropertyName("isReflect")]
    public bool? IsReflect { get; set; }

    [JsonPropertyName("isLightScreen")]
    public bool? IsLightScreen { get; set; }

    [JsonPropertyName("isAuroraVeil")]
    public bool? IsAuroraVeil { get; set; }

    [JsonPropertyName("isStealthRock")]
    public bool? IsStealthRock { get; set; }

    [JsonPropertyName("spikes")]
    public int? Spikes { get; set; }

    [JsonPropertyName("isHelpingHand")]
    public bool? IsHelpingHand { get; set; }

    [JsonPropertyName("isTailwind")]
    public bool? IsTailwind { get; set; }

    [JsonPropertyName("isFriendGuard")]
    public bool? IsFriendGuard { get; set; }
}
