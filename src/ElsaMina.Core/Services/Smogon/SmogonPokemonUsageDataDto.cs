using Newtonsoft.Json;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonPokemonUsageDataDto
{
    [JsonProperty(nameof(Abilities))]
    public Dictionary<string, double> Abilities { get; set; }

    [JsonProperty(nameof(Items))]
    public Dictionary<string, double> Items { get; set; }

    [JsonProperty(nameof(Spreads))]
    public Dictionary<string, double> Spreads { get; set; }

    [JsonProperty(nameof(Moves))]
    public Dictionary<string, double> Moves { get; set; }

    [JsonProperty(nameof(Teammates))]
    public Dictionary<string, double> Teammates { get; set; }

    [JsonProperty("usage")]
    public double Usage { get; set; }

    [JsonProperty("Raw count")]
    public int RawCount { get; set; }

    [JsonProperty("Checks and Counters")]
    public Dictionary<string, double[]> ChecksAndCounters { get; set; }
}
