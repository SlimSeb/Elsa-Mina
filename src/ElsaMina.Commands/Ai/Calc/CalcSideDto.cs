using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Field conditions affecting one side of the battle (the attacker's or the defender's).
/// </summary>
public class CalcSideDto
{
    [JsonProperty("isReflect")]
    public bool? IsReflect { get; set; }

    [JsonProperty("isLightScreen")]
    public bool? IsLightScreen { get; set; }

    [JsonProperty("isAuroraVeil")]
    public bool? IsAuroraVeil { get; set; }

    [JsonProperty("isStealthRock")]
    public bool? IsStealthRock { get; set; }

    [JsonProperty("spikes")]
    public int? Spikes { get; set; }

    [JsonProperty("isHelpingHand")]
    public bool? IsHelpingHand { get; set; }

    [JsonProperty("isTailwind")]
    public bool? IsTailwind { get; set; }

    [JsonProperty("isFriendGuard")]
    public bool? IsFriendGuard { get; set; }
}
