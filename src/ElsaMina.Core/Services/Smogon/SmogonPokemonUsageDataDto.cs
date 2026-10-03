using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonPokemonUsageDataDto
{
    [JsonPropertyName(nameof(Abilities))]
    public Dictionary<string, double> Abilities { get; set; }

    [JsonPropertyName(nameof(Items))]
    public Dictionary<string, double> Items { get; set; }

    [JsonPropertyName(nameof(Spreads))]
    public Dictionary<string, double> Spreads { get; set; }

    [JsonPropertyName(nameof(Moves))]
    public Dictionary<string, double> Moves { get; set; }

    [JsonPropertyName(nameof(Teammates))]
    public Dictionary<string, double> Teammates { get; set; }

    [JsonPropertyName("usage")]
    public double Usage { get; set; }

    [JsonPropertyName("Raw count")]
    public int RawCount { get; set; }

    [JsonPropertyName("Checks and Counters")]
    public Dictionary<string, double[]> ChecksAndCounters { get; set; }
}
