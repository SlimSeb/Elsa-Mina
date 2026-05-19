using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class Move
{
    [JsonProperty("move")]
    public string Name { get; set; } = "";

    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("pp")]
    public int Pp { get; set; }

    [JsonProperty("maxpp")]
    public int MaxPp { get; set; }

    [JsonProperty("target")]
    public string Target { get; set; } = "";

    [JsonProperty("disabled")]
    public bool Disabled { get; set; }
}
