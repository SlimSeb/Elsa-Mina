namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// An action our side can take during a simulated turn.
/// For a move, MemberIndex is the acting team member and MoveListIndex points into its move list.
/// For a switch, MemberIndex is the team member switched in and MoveListIndex is unused.
/// </summary>
public sealed record SimulationAction(
    SimulationActionKind Kind,
    int MemberIndex,
    int MoveListIndex = 0,
    bool UseTerastallize = false);
