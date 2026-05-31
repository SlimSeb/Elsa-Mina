namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// A (main or side) pot: an amount of chips together with the players eligible to win it.
/// </summary>
public sealed class PokerPot
{
    public PokerPot(long amount, IReadOnlyList<string> eligiblePlayerIds)
    {
        Amount = amount;
        EligiblePlayerIds = eligiblePlayerIds;
    }

    public long Amount { get; }

    public IReadOnlyList<string> EligiblePlayerIds { get; }
}
