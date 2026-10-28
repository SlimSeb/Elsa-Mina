using System.Text.Json.Nodes;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// Plays turns on Showdown's simulator (tools/minipoke/search_sim.js).
/// </summary>
public interface IShowdownSimulator
{
    /// <summary>
    /// Whether the simulator is configured and installed.
    /// </summary>
    bool IsAvailable { get; }

    Task<SimulationReply> SimulateAsync(JsonObject request, CancellationToken cancellationToken = default);
}
