using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class ActivePokemon
{
    [JsonProperty("moves")]
    public List<Move> Moves { get; set; } = new();

    [JsonProperty("canTerastallize")]
    public string CanTerastallize { get; set; } = "";

    [JsonProperty("trapped")]
    public bool Trapped { get; set; }
}
