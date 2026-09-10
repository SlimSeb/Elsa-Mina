using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class Side
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("pokemon")]
    public List<SidePokemon> Pokemon { get; set; } = new();
}
