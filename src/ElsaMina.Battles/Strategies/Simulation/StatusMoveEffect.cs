namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// The board effect of one of our status moves, used to give status moves value in the search
/// (they deal no damage, so without this they are always dominated by attacking).
/// </summary>
public enum StatusMoveEffect
{
    None,
    StealthRock,
    Spikes,
    ToxicSpikes,
    StickyWeb,
    Taunt,
    OtherStatus
}
