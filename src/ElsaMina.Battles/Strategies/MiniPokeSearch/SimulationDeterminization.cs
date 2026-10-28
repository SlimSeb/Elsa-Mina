using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// One guess of the opponent's sets: its actions, and the simulated turns of each of our candidates against each of
/// them (a sample is null when Showdown refused one of the two choices).
/// </summary>
public sealed record SimulationDeterminization(
    [property: JsonPropertyName("theirs")] List<string> Theirs,
    [property: JsonPropertyName("leaves")] List<List<List<SimulationLeaf>>> Leaves);
