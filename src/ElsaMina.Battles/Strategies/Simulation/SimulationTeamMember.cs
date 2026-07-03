namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// One of our alive team members in the simulation, with precomputed speed and move damage.
/// </summary>
public class SimulationTeamMember
{
    /// <summary>
    /// 1-based slot in the side's team, used to build the /choose switch command.
    /// </summary>
    public int TeamSlot { get; init; }

    public string Species { get; init; } = "";
    public double InitialHpRatio { get; init; }
    public int Speed { get; init; }

    /// <summary>
    /// Fraction of max HP this member loses to entry hazards when switched in
    /// (Stealth Rock scaled by Rock effectiveness, plus Spikes when grounded).
    /// </summary>
    public double SwitchInChipRatio { get; init; }

    public List<SimulationMove> Moves { get; init; } = [];
}
