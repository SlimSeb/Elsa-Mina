using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

public class Name
{
    [JsonPropertyName("fr")]
    public string French { get; set; }

    [JsonPropertyName("en")]
    public string English { get; set; }

    [JsonPropertyName("jp")]
    public string Japanese { get; set; }
}

public class Sprite
{
    [JsonPropertyName("regular")]
    public string Regular { get; set; }

    [JsonPropertyName("shiny")]
    public string Shiny { get; set; }
}

public class PokemonType
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }
}

public class Talent
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("tc")]
    public bool IsHidden { get; set; }
}

public class Stats
{
    [JsonPropertyName("hp")]
    public int HP { get; set; }

    [JsonPropertyName("atk")]
    public int Attack { get; set; }

    [JsonPropertyName("def")]
    public int Defense { get; set; }

    [JsonPropertyName("spe_atk")]
    public int SpecialAttack { get; set; }

    [JsonPropertyName("spe_def")]
    public int SpecialDefense { get; set; }

    [JsonPropertyName("vit")]
    public int Speed { get; set; }
}

public class Resistance
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("multiplier")]
    public double Multiplier { get; set; }
}

public class EvolutionNext
{
    [JsonPropertyName("pokedex_id")]
    public int PokedexId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("condition")]
    public string Condition { get; set; }
}

public class Evolution
{
    [JsonPropertyName("pre")]
    public object PreEvolution { get; set; }

    [JsonPropertyName("next")]
    public List<EvolutionNext> NextEvolutions { get; set; }

    [JsonPropertyName("mega")]
    public object MegaEvolution { get; set; }
}

public class Gender
{
    [JsonPropertyName("male")]
    public double Male { get; set; }

    [JsonPropertyName("female")]
    public double Female { get; set; }
}

public class Pokemon
{
    [JsonPropertyName("pokedex_id")]
    public int PokedexId { get; set; }

    [JsonPropertyName("generation")]
    public int Generation { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("name")]
    public Name Name { get; set; }

    [JsonPropertyName("sprites")]
    public Sprite Sprites { get; set; }

    [JsonPropertyName("types")]
    public List<PokemonType> Types { get; set; }

    [JsonPropertyName("talents")]
    public List<Talent> Talents { get; set; }

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; }

    [JsonPropertyName("resistances")]
    public List<Resistance> Resistances { get; set; }

    [JsonPropertyName("evolution")]
    public Evolution Evolution { get; set; }

    [JsonPropertyName("height")]
    public string Height { get; set; }

    [JsonPropertyName("weight")]
    public string Weight { get; set; }

    [JsonPropertyName("egg_groups")]
    public List<string> EggGroups { get; set; }

    [JsonPropertyName("sexe")]
    public Gender Gender { get; set; }

    [JsonPropertyName("catch_rate")]
    public int? CatchRate { get; set; }

    [JsonPropertyName("level_100")]
    public int? Level100Experience { get; set; }

    [JsonPropertyName("formes")]
    public object Formes { get; set; }
}
