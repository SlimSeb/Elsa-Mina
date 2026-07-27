namespace ElsaMina.Commands.Games.President;

/// <summary>
/// The rules of président that depend only on cards: which combinations a hand may put on the pile,
/// whether it can satisfy the "ou rien" constraint, and when a play closes a square. Everything here
/// is a pure function of its arguments.
/// </summary>
public static class PresidentRules
{
    /// <summary>
    /// The (rank, card count) combinations the given hand may legally put on the pile: any set of
    /// same-ranked cards when leading, otherwise exactly the pile's card count at an equal or higher
    /// rank. Under the "ou rien" rule, only the pile's exact rank is playable.
    /// </summary>
    public static IReadOnlyList<(int Rank, int Count)> GetLegalPlays(IReadOnlyCollection<PresidentCard> hand,
        PresidentTrick trick, bool matchRequired)
    {
        if (matchRequired && !trick.IsEmpty)
        {
            return CanMatchPile(hand, trick) ? [(trick.TopRank.Value, trick.RequiredCount)] : [];
        }

        var plays = new List<(int Rank, int Count)>();
        var rankGroups = hand
            .GroupBy(card => card.Rank)
            .OrderBy(group => group.Key);

        foreach (var group in rankGroups)
        {
            var available = group.Count();
            if (trick.IsEmpty)
            {
                for (var count = 1; count <= available; count++)
                {
                    plays.Add((group.Key, count));
                }
            }
            else if (available >= trick.RequiredCount && group.Key >= trick.TopRank)
            {
                plays.Add((group.Key, trick.RequiredCount));
            }
        }

        return plays;
    }

    /// <summary>
    /// Whether the hand holds enough cards of the pile's exact rank to satisfy the "ou rien" rule.
    /// </summary>
    public static bool CanMatchPile(IReadOnlyCollection<PresidentCard> hand, PresidentTrick trick) =>
        !trick.IsEmpty && hand.Count(card => card.Rank == trick.TopRank) >= trick.RequiredCount;

    /// <summary>
    /// True when the trailing consecutive plays of the pile all share the given rank and add up to
    /// all four cards of it: the fourth single closing an "ou rien" chain, the complementary pair
    /// laid on a pair, or a four-card lead.
    /// </summary>
    public static bool CompletesSquare(PresidentTrick trick, int rank)
    {
        var consecutiveCards = 0;
        for (var playIndex = trick.Plays.Count - 1; playIndex >= 0; playIndex--)
        {
            var (_, cards) = trick.Plays[playIndex];
            if (cards[0].Rank != rank)
            {
                break;
            }

            consecutiveCards += cards.Count;
        }

        return consecutiveCards == 4;
    }

    /// <summary>
    /// Whether the given play beats what is currently on the pile: the same number of cards at an
    /// equal or higher rank, and the pile's exact rank while "ou rien" pins the player. Rejections are
    /// reported one by one so the caller can explain which rule the play broke.
    /// </summary>
    public static PresidentPlayRejection BeatsCurrentPlay(PresidentTrick trick, int rank, int count,
        bool matchRequired)
    {
        if (trick.IsEmpty)
        {
            return PresidentPlayRejection.None;
        }

        if (count != trick.RequiredCount)
        {
            return PresidentPlayRejection.WrongCount;
        }

        if (rank < trick.TopRank)
        {
            return PresidentPlayRejection.TooLow;
        }

        return matchRequired && rank != trick.TopRank
            ? PresidentPlayRejection.MustMatch
            : PresidentPlayRejection.None;
    }
}
