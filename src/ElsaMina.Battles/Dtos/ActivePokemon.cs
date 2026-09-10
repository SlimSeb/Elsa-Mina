using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class ActivePokemon
{
    [JsonPropertyName("moves")]
    public List<Move> Moves { get; set; } = new();

    [JsonPropertyName("canTerastallize")]
    public string CanTerastallize { get; set; } = "";

    [JsonPropertyName("trapped")]
    public bool Trapped { get; set; }
}
