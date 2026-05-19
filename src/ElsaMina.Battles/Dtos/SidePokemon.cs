using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class SidePokemon
{
    [JsonProperty("ident")]
    public string Ident { get; set; } = "";

    [JsonProperty("details")]
    public string Details { get; set; } = "";

    [JsonProperty("condition")]
    public string Condition { get; set; } = "";

    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("stats")]
    public Stats Stats { get; set; } = new();

    [JsonProperty("moves")]
    public List<string> Moves { get; set; } = new();

    [JsonProperty("baseAbility")]
    public string BaseAbility { get; set; } = "";

    [JsonProperty("item")]
    public string Item { get; set; } = "";

    [JsonProperty("pokeball")]
    public string Pokeball { get; set; } = "";

    [JsonProperty("ability")]
    public string Ability { get; set; } = "";

    [JsonProperty("commanding")]
    public bool Commanding { get; set; }

    [JsonProperty("reviving")]
    public bool Reviving { get; set; }

    [JsonProperty("teraType")]
    public string TeraType { get; set; } = "";

    [JsonProperty("terastallized")]
    public string Terastallized { get; set; } = "";
}
