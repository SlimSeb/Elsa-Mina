using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

public sealed class MoveData
{
    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("accuracy")]
    public object Accuracy { get; set; } = true;

    [JsonPropertyName("basePower")]
    public int BasePower { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("isNonstandard")]
    public string IsNonstandard { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("pp")]
    public int Pp { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("flags")]
    public Dictionary<string, int> Flags { get; set; } = new();

    [JsonPropertyName("isZ")]
    public string IsZ { get; set; } = string.Empty;

    [JsonPropertyName("critRatio")]
    public int CritRatio { get; set; }

    [JsonPropertyName("secondary")]
    public SecondaryEffect Secondary { get; set; } = new();

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("contestType")]
    public string ContestType { get; set; } = string.Empty;

    [JsonPropertyName("boosts")]
    public StatBoosts Boosts { get; set; } = new();

    [JsonPropertyName("drain")]
    public int[] Drain { get; set; } = Array.Empty<int>();

    [JsonPropertyName("zMove")]
    public ZMoveInfo ZMove { get; set; } = new();
}

public sealed class SecondaryEffect
{
    [JsonPropertyName("chance")]
    public int Chance { get; set; }

    [JsonPropertyName("boosts")]
    public StatBoosts Boosts { get; set; } = new();
}

public sealed class StatBoosts
{
    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("atk")]
    public int Atk { get; set; }

    [JsonPropertyName("def")]
    public int Def { get; set; }

    [JsonPropertyName("spa")]
    public int Spa { get; set; }

    [JsonPropertyName("spd")]
    public int Spd { get; set; }

    [JsonPropertyName("spe")]
    public int Spe { get; set; }

    [JsonPropertyName("accuracy")]
    public int Accuracy { get; set; }

    [JsonPropertyName("evasion")]
    public int Evasion { get; set; }
}

public sealed class ZMoveInfo
{
    [JsonPropertyName("effect")]
    public string Effect { get; set; } = string.Empty;
}
