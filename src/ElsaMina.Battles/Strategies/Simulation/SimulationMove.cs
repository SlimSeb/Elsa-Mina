namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// A move of one of our team members, with its damage output precomputed against the opponent's active pokemon.
/// Damage ratios are expressed as a fraction of the defender's max HP.
/// </summary>
public class SimulationMove
{
    public string Name { get; init; } = "";

    /// <summary>
    /// 1-based index in the move request, used to build the /choose command.
    /// 0 for bench members, whose moves cannot be selected this turn.
    /// </summary>
    public int RequestMoveIndex { get; init; }

    public int Priority { get; init; }
    public bool IsStatus { get; init; }
    public double DamageRatio { get; init; }

    /// <summary>
    /// Damage ratio when the user is terastallized. Only computed for the active pokemon.
    /// </summary>
    public double? TeraDamageRatio { get; init; }
}
