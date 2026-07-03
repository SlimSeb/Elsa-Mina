namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Precomputed model of the current 1v1 battle situation used by the search algorithms.
/// All damage calc invocations happen once when the model is built, so turn resolution
/// during the search is a matter of array lookups.
/// </summary>
public class SimulationModel
{
    public List<SimulationTeamMember> Members { get; init; } = [];

    /// <summary>
    /// Index into Members of our active pokemon, or -1 when we must switch (forced switch).
    /// </summary>
    public int ActiveMemberIndex { get; init; } = -1;

    public bool CanTerastallize { get; init; }
    public bool ActiveIsTrapped { get; init; }

    public List<OpponentSimulationMove> OpponentMoves { get; init; } = [];
    public int OpponentSpeed { get; init; }
    public double OpponentHpRatio { get; init; }
    public int OpponentBenchAliveCount { get; init; }

    public SimulationState CreateInitialState()
    {
        var memberHpRatios = Members.Select(member => member.InitialHpRatio).ToArray();
        return new SimulationState(ActiveMemberIndex, memberHpRatios, OpponentHpRatio,
            HasTerastallized: false, RootActiveIsTerastallized: false);
    }
}
