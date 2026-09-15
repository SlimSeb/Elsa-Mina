namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// Evaluates the best five-card poker hand out of a set of cards (typically the seven made of a
/// player's two hole cards plus the five community cards).
/// </summary>
public static class PokerHandEvaluator
{
    private const int HAND_SIZE = 5;

    /// <summary>
    /// Returns the strongest five-card hand that can be made from the given cards. Requires at
    /// least five cards.
    /// </summary>
    public static PokerHandEvaluation EvaluateBest(IReadOnlyList<PokerCard> cards)
    {
        if (cards is null || cards.Count < HAND_SIZE)
        {
            throw new ArgumentException("At least five cards are required to evaluate a poker hand.", nameof(cards));
        }

        PokerHandEvaluation best = null;

        // Enumerate every 5-card combination and keep the strongest evaluation.
        foreach (var hand in EnumerateHands(cards, new PokerCard[HAND_SIZE], 0, 0))
        {
            var evaluation = EvaluateFive(hand);
            if (best is null || evaluation.CompareTo(best) > 0)
            {
                best = evaluation;
            }
        }

        return best;
    }

    /// <summary>
    /// Enumerates every combination of <see cref="HAND_SIZE"/> cards, in ascending index order. The
    /// yielded hand is a snapshot, so it stays valid once the enumeration has moved on.
    /// </summary>
    private static IEnumerable<PokerCard[]> EnumerateHands(IReadOnlyList<PokerCard> cards, PokerCard[] hand,
        int slot, int firstIndex)
    {
        if (slot == hand.Length)
        {
            yield return (PokerCard[])hand.Clone();
            yield break;
        }

        var lastIndex = cards.Count - (hand.Length - slot);
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            hand[slot] = cards[index];
            foreach (var combination in EnumerateHands(cards, hand, slot + 1, index + 1))
            {
                yield return combination;
            }
        }
    }

    /// <summary>
    /// Evaluates exactly five cards.
    /// </summary>
    public static PokerHandEvaluation EvaluateFive(IReadOnlyList<PokerCard> cards)
    {
        if (cards is null || cards.Count != HAND_SIZE)
        {
            throw new ArgumentException("Exactly five cards are required.", nameof(cards));
        }

        var ordered = cards.OrderByDescending(card => card.Rank).ToList();
        var isFlush = ordered.All(card => card.Suit == ordered[0].Suit);

        var straightHigh = GetStraightHighCard(ordered);
        var isStraight = straightHigh.HasValue;

        // Group ranks by how many times each appears, ordered by count then rank (both descending).
        var groups = ordered
            .GroupBy(card => card.Rank)
            .Select(group => (Rank: group.Key, Count: group.Count()))
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.Rank)
            .ToList();

        if (isStraight && isFlush)
        {
            return new PokerHandEvaluation(PokerHandRank.StraightFlush, [straightHigh.Value], ordered);
        }

        if (groups[0].Count == 4)
        {
            var kicker = groups[1].Rank;
            return new PokerHandEvaluation(PokerHandRank.FourOfAKind, [groups[0].Rank, kicker], ordered);
        }

        if (groups[0].Count == 3 && groups[1].Count == 2)
        {
            return new PokerHandEvaluation(PokerHandRank.FullHouse, [groups[0].Rank, groups[1].Rank], ordered);
        }

        if (isFlush)
        {
            return new PokerHandEvaluation(PokerHandRank.Flush, ordered.Select(card => card.Rank).ToList(), ordered);
        }

        if (isStraight)
        {
            return new PokerHandEvaluation(PokerHandRank.Straight, [straightHigh.Value], ordered);
        }

        if (groups[0].Count == 3)
        {
            var kickers = groups.Skip(1).Select(group => group.Rank).ToList();
            return new PokerHandEvaluation(PokerHandRank.ThreeOfAKind, [groups[0].Rank, .. kickers], ordered);
        }

        if (groups[0].Count == 2 && groups[1].Count == 2)
        {
            var kicker = groups[2].Rank;
            return new PokerHandEvaluation(PokerHandRank.TwoPair, [groups[0].Rank, groups[1].Rank, kicker], ordered);
        }

        if (groups[0].Count == 2)
        {
            var kickers = groups.Skip(1).Select(group => group.Rank).ToList();
            return new PokerHandEvaluation(PokerHandRank.Pair, [groups[0].Rank, .. kickers], ordered);
        }

        return new PokerHandEvaluation(PokerHandRank.HighCard, ordered.Select(card => card.Rank).ToList(), ordered);
    }

    /// <summary>
    /// If the five (rank-descending) cards form a straight, returns its high card; otherwise null.
    /// Handles the Ace-low "wheel" (A-2-3-4-5), whose high card is the five.
    /// </summary>
    private static int? GetStraightHighCard(IReadOnlyList<PokerCard> orderedDescending)
    {
        var distinctRanks = orderedDescending.Select(card => card.Rank).Distinct().ToList();
        if (distinctRanks.Count != 5)
        {
            return null;
        }

        if (distinctRanks[0] - distinctRanks[4] == 4)
        {
            return distinctRanks[0];
        }

        // Wheel: A, 5, 4, 3, 2 -> straight to the five.
        if (distinctRanks[0] == PokerCard.ACE && distinctRanks[1] == 5 && distinctRanks[4] == 2)
        {
            return 5;
        }

        return null;
    }
}
