using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

public sealed class PokedexEntry
{
    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("types")]
    public string[] Types { get; set; }

    [JsonPropertyName("baseStats")]
    public BaseStats BaseStats { get; set; }

    [JsonPropertyName("abilities")]
    public Dictionary<string, string> Abilities { get; set; }

    [JsonPropertyName("heightm")]
    public double? Heightm { get; set; }

    [JsonPropertyName("weightkg")]
    public double? Weightkg { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; }

    [JsonPropertyName("evos")]
    public string[] Evos { get; set; }

    [JsonPropertyName("prevo")]
    public string Prevo { get; set; }

    [JsonPropertyName("evoLevel")]
    public int EvoLevel { get; set; }

    [JsonPropertyName("evoType")]
    public string EvoType { get; set; }

    [JsonPropertyName("evoItem")]
    public string EvoItem { get; set; }

    [JsonPropertyName("evoCondition")]
    public string EvoCondition { get; set; }

    [JsonPropertyName("eggGroups")]
    public string[] EggGroups { get; set; }

    // Formes / variants
    [JsonPropertyName("baseSpecies")]
    public string BaseSpecies { get; set; }

    [JsonPropertyName("forme")]
    public string Forme { get; set; }

    [JsonPropertyName("otherFormes")]
    public string[] OtherFormes { get; set; }

    [JsonPropertyName("formeOrder")]
    public string[] FormeOrder { get; set; }

    [JsonPropertyName("gender")]
    public string Gender { get; set; }

    [JsonPropertyName("gen")]
    public int Gen { get; set; }

    [JsonPropertyName("requiredItem")]
    public string RequiredItem { get; set; }
}

public sealed class BaseStats
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
}
