using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class SidePokemon
{
    [JsonPropertyName("ident")]
    public string Ident { get; set; } = "";

    [JsonPropertyName("details")]
    public string Details { get; set; } = "";

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = "";

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = new();

    [JsonPropertyName("moves")]
    public List<string> Moves { get; set; } = new();

    [JsonPropertyName("baseAbility")]
    public string BaseAbility { get; set; } = "";

    [JsonPropertyName("item")]
    public string Item { get; set; } = "";

    [JsonPropertyName("pokeball")]
    public string Pokeball { get; set; } = "";

    [JsonPropertyName("ability")]
    public string Ability { get; set; } = "";

    [JsonPropertyName("commanding")]
    public bool Commanding { get; set; }

    [JsonPropertyName("reviving")]
    public bool Reviving { get; set; }

    [JsonPropertyName("teraType")]
    public string TeraType { get; set; } = "";

    [JsonPropertyName("terastallized")]
    public string Terastallized { get; set; } = "";
}
