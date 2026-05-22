using Newtonsoft.Json;

namespace ElsaMina.Commands.Showdown.SmogonStats;

public class SmogonPokemonUsageDataDto
{
    [JsonProperty("Abilities")]
    public Dictionary<string, double> Abilities { get; set; }

    [JsonProperty("Items")]
    public Dictionary<string, double> Items { get; set; }

    [JsonProperty("Spreads")]
    public Dictionary<string, double> Spreads { get; set; }

    [JsonProperty("Moves")]
    public Dictionary<string, double> Moves { get; set; }

    [JsonProperty("Teammates")]
    public Dictionary<string, double> Teammates { get; set; }

    [JsonProperty("usage")]
    public double Usage { get; set; }

    [JsonProperty("Raw count")]
    public int RawCount { get; set; }

    [JsonProperty("Checks and Counters")]
    public Dictionary<string, double[]> ChecksAndCounters { get; set; }
}
