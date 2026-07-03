namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Mutable-free snapshot of a simulated battle line.
/// ActiveMemberIndex is -1 when our side has no active pokemon and must switch (forced switch).
/// RootActiveIsTerastallized tracks whether the pokemon active at the root of the search has
/// terastallized during this line; Terastallization persists for the rest of the battle.
/// </summary>
public sealed record SimulationState(
    int ActiveMemberIndex,
    double[] MemberHpRatios,
    double OpponentHpRatio,
    bool HasTerastallized,
    bool RootActiveIsTerastallized);
