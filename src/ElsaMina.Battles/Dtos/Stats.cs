using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class Stats
{
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
