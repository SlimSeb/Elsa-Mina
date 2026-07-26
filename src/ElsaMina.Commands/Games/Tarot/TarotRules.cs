using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// The rules of French Tarot that depend only on cards: which moves are legal, which declarations a
/// hand allows, and how the petit au bout is awarded. Everything here is a pure function of its
/// arguments, so it can be reasoned about (and tested) without a running game.
/// </summary>
public static class TarotRules
{
    /// <summary>
    /// The cards the given hand may legally play to the current trick.
    /// </summary>
    /// <param name="hand">The player's remaining cards.</param>
    /// <param name="trick">The trick in progress.</param>
    /// <param name="playerCount">How many players are seated, which decides whether a king was called.</param>
    /// <param name="calledKing">The card the taker called in a five-handed game, or <c>null</c>.</param>
    /// <param name="trickNumber">The 1-based number of the current trick.</param>
    public static IReadOnlyCollection<TarotCard> GetLegalMoves(List<TarotCard> hand, TarotTrick trick,
        int playerCount, TarotCard calledKing, int trickNumber)
    {
        var excuse = hand.FirstOrDefault(card => card.IsExcuse);

        // Leading, or only the Excuse has been played so far: anything goes, except that in a
        // five-handed game the suit of the call may not be led.
        if (trick.IsEmpty || trick.LeadCard is null)
        {
            return GetLegalLeadMoves(hand, playerCount, calledKing, trickNumber);
        }

        var legal = new List<TarotCard>();
        var leadCard = trick.LeadCard;
        var highestTrump = trick.HighestTrumpRank;
        var trumps = hand.Where(card => card.IsTrump).ToList();

        if (leadCard.IsTrump)
        {
            AddTrumpMoves(legal, trumps, highestTrump, hand);
        }
        else
        {
            var suitCards = hand.Where(card => card.Suit == leadCard.Suit).ToList();
            if (suitCards.Count > 0)
            {
                legal.AddRange(suitCards);
            }
            else
            {
                AddTrumpMoves(legal, trumps, highestTrump, hand);
            }
        }

        if (excuse is not null && !legal.Contains(excuse))
        {
            legal.Add(excuse);
        }

        return legal;
    }

    /// <summary>
    /// The cards a player may lead a trick with. In a five-handed game the suit of the called card
    /// cannot be led on the very first trick, the only exception being the called card itself (and a
    /// fallback when the player holds nothing but cards of the called suit).
    /// </summary>
    public static List<TarotCard> GetLegalLeadMoves(List<TarotCard> hand, int playerCount,
        TarotCard calledKing, int trickNumber)
    {
        if (playerCount != 5 || calledKing is null || trickNumber != 1)
        {
            return hand.ToList();
        }

        var leadable = hand
            .Where(card => card.Suit != calledKing.Suit || card == calledKing)
            .ToList();

        return leadable.Count > 0 ? leadable : hand.ToList();
    }

    /// <summary>
    /// Adds the trumps that may answer a trump lead (or a cut): over-trumping is compulsory when
    /// possible, and a player holding no trump at all may throw anything but the Excuse.
    /// </summary>
    public static void AddTrumpMoves(List<TarotCard> legal, List<TarotCard> trumps, int? highestTrump,
        List<TarotCard> hand)
    {
        if (trumps.Count == 0)
        {
            legal.AddRange(hand.Where(card => !card.IsExcuse));
            return;
        }

        var overtrumps = trumps.Where(card => highestTrump is null || card.Rank > highestTrump).ToList();
        legal.AddRange(overtrumps.Count > 0 ? overtrumps : trumps);
    }

    /// <summary>
    /// The poignée tier (1 single, 2 double, 3 triple, 0 none) the given hand could declare. The Excuse
    /// may stand in for a missing trump to reach a tier.
    /// </summary>
    public static int GetDeclarablePoigneeTier(IReadOnlyCollection<TarotCard> hand, int playerCount)
    {
        var thresholds = TarotConstants.POIGNEE_THRESHOLDS[playerCount];
        var trumpCount = hand.Count(card => card.IsTrump);
        var hasExcuse = hand.Any(card => card.IsExcuse);

        var tier = TierForTrumpCount(trumpCount, thresholds);
        if (hasExcuse)
        {
            tier = Math.Max(tier, TierForTrumpCount(trumpCount + 1, thresholds));
        }

        return tier;
    }

    public static int TierForTrumpCount(int count, int[] thresholds)
    {
        if (count >= thresholds[2])
        {
            return 3;
        }

        if (count >= thresholds[1])
        {
            return 2;
        }

        return count >= thresholds[0] ? 1 : 0;
    }

    /// <summary>
    /// The misère types the given hand could declare: a misère d'atout when it holds no trump (the
    /// Excuse is tolerated), a misère de tête when it holds no face card.
    /// </summary>
    public static IReadOnlyList<TarotMisereType> GetDeclarableMisereTypes(IReadOnlyCollection<TarotCard> hand)
    {
        var types = new List<TarotMisereType>();
        if (hand.All(card => !card.IsTrump))
        {
            types.Add(TarotMisereType.Trump);
        }

        if (hand.All(card => !card.IsFaceCard))
        {
            types.Add(TarotMisereType.Head);
        }

        return types;
    }

    /// <summary>
    /// Which side won the Petit in the last trick: +1 for the taker's side, -1 for the defenders,
    /// 0 when the Petit was not in that trick at all.
    /// </summary>
    public static int ComputePetitAuBoutSide(TarotTrick lastTrick, TarotPlayer lastTrickWinner)
    {
        if (lastTrick is null || lastTrickWinner is null)
        {
            return 0;
        }

        var petitInLastTrick = lastTrick.Plays
            .Any(play => play.Card.IsTrump && play.Card.Rank == TarotCard.PETIT);
        if (!petitInLastTrick)
        {
            return 0;
        }

        return lastTrickWinner.IsTaker || lastTrickWinner.IsPartner ? 1 : -1;
    }

    /// <summary>
    /// Whether the given hand holds all four kings, in which case the taker must call a queen instead
    /// to find a partner.
    /// </summary>
    public static bool HoldsAllKings(IReadOnlyCollection<TarotCard> hand) =>
        TarotConstants.Suits.All(suit => hand.Contains(TarotCard.Suited(suit, TarotCard.KING)));
}
