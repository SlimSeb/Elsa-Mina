namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// A move the opponent's active pokemon is expected to use, with its damage
/// precomputed against every one of our team members.
/// </summary>
public class OpponentSimulationMove
{
    public string Name { get; init; } = "";
    public int Priority { get; init; }
    public double Probability { get; init; }

    /// <summary>
    /// Damage ratio dealt to each of our team members, indexed like SimulationModel.Members.
    /// </summary>
    public double[] DamageToMembers { get; init; } = [];

    /// <summary>
    /// Damage ratio dealt to our active pokemon once it is terastallized (defensive typing changes).
    /// </summary>
    public double DamageToTerastallizedActive { get; init; }
}
