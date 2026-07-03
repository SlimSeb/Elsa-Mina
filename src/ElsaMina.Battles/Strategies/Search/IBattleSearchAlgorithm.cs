using ElsaMina.Battles.Strategies.Simulation;

namespace ElsaMina.Battles.Strategies.Search;

public interface IBattleSearchAlgorithm
{
    /// <summary>
    /// Searches the simulation model for the best action our side can take this turn.
    /// Returns null when no action is available.
    /// </summary>
    SimulationAction FindBestAction(SimulationModel model);
}
