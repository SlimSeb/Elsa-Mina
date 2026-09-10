using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class Move
{
    [JsonPropertyName("move")]
    public string Name { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("pp")]
    public int Pp { get; set; }

    [JsonPropertyName("maxpp")]
    public int MaxPp { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}
