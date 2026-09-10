using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams;

public class PokemonSet
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("species")]
    public string Species { get; set; }
    [JsonPropertyName("gender")]
    public string Gender { get; set; }
    [JsonPropertyName("item")]
    public string Item { get; set; }
    [JsonPropertyName("ability")]
    public string Ability { get; set; }
    [JsonPropertyName("shiny")]
    public bool IsShiny { get; set; }
    [JsonPropertyName("level")]
    public int Level { get; set; }
    [JsonPropertyName("happiness")]
    public int Happiness { get; set; } = -1;
    [JsonPropertyName("pokeball")]
    public string Pokeball { get; set; }
    [JsonPropertyName("hpType")]
    public string HiddenPowerType { get; set; }
    [JsonPropertyName("teraType")]
    public string TeraType { get; set; }
    [JsonPropertyName("dynamaxLevel")]
    public int DynamaxLevel { get; set; } = -1;
    [JsonPropertyName("gigantamax")]
    public bool IsGigantamax { get; set; }
    [JsonPropertyName("nature")]
    public string Nature { get; set; }
    [JsonPropertyName("evs")]
    public IDictionary<string, int> EffortValues { get; set; }
    [JsonPropertyName("ivs")]
    public IDictionary<string, int> IndividualValues { get; set; }
    [JsonPropertyName("moves")]
    public ICollection<string> Moves { get; set; }
}