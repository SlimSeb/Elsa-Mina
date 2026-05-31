namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// The strength of a player's best five-card hand. Two evaluations are compared first on
/// <see cref="Rank"/> then on <see cref="Tiebreakers"/> (high ranks first), so the natural
/// ordering goes from weakest to strongest hand.
/// </summary>
public sealed class PokerHandEvaluation : IComparable<PokerHandEvaluation>
{
    public PokerHandEvaluation(PokerHandRank rank, IReadOnlyList<int> tiebreakers, IReadOnlyList<PokerCard> cards)
    {
        Rank = rank;
        Tiebreakers = tiebreakers;
        Cards = cards;
    }

    public PokerHandRank Rank { get; }

    /// <summary>
    /// Ordered rank values used to break ties between hands of the same <see cref="Rank"/>,
    /// most significant first (e.g. for a full house: trips rank then pair rank).
    /// </summary>
    public IReadOnlyList<int> Tiebreakers { get; }

    /// <summary>
    /// The five cards that make up the hand.
    /// </summary>
    public IReadOnlyList<PokerCard> Cards { get; }

    /// <summary>
    /// True for the Ace-high variant of a straight flush.
    /// </summary>
    public bool IsRoyalFlush => Rank == PokerHandRank.StraightFlush && Tiebreakers.Count > 0
                                && Tiebreakers[0] == PokerCard.ACE;

    public int CompareTo(PokerHandEvaluation other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Rank != other.Rank)
        {
            return Rank.CompareTo(other.Rank);
        }

        var length = Math.Min(Tiebreakers.Count, other.Tiebreakers.Count);
        for (var i = 0; i < length; i++)
        {
            var comparison = Tiebreakers[i].CompareTo(other.Tiebreakers[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return Tiebreakers.Count.CompareTo(other.Tiebreakers.Count);
    }
}
