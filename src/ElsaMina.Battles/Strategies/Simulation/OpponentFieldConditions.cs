namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Board state on the opponent's side that our status moves can create: entry hazards that will chip
/// or cripple the opponent's future switch-ins, and whether the opponent's active pokemon is taunted.
/// </summary>
public sealed record OpponentFieldConditions
{
    public static readonly OpponentFieldConditions Empty = new();

    public bool StealthRock { get; init; }
    public int SpikesLayers { get; init; }
    public bool ToxicSpikes { get; init; }
    public bool StickyWeb { get; init; }
    public bool Taunted { get; init; }
}
