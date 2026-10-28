using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// search_sim.js's answer to one request.
/// </summary>
public sealed record SimulationReply(
    [property: JsonPropertyName("determinizations")] List<SimulationDeterminization> Determinizations,
    [property: JsonPropertyName("error")] string Error);
