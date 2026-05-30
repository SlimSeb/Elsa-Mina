namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// A single trick: the cards played by each player in order, with the logic to find the winner.
/// </summary>
public sealed class TarotTrick
{
    public List<(TarotPlayer Player, TarotCard Card)> Plays { get; } = [];

    public bool IsEmpty => Plays.Count == 0;

    /// <summary>
    /// The suit that must be followed: the suit of the first non-Excuse card played.
    /// Returns <c>null</c> while the trick only contains the Excuse (or is empty).
    /// </summary>
    public TarotSuit? LeadSuit
    {
        get
        {
            foreach (var (_, card) in Plays)
            {
                if (!card.IsExcuse)
                {
                    return card.Suit;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The highest trump currently in the trick, or <c>null</c> if none has been played.
    /// </summary>
    public int? HighestTrumpRank
    {
        get
        {
            int? highest = null;
            foreach (var (_, card) in Plays)
            {
                if (card.IsTrump && (highest is null || card.Rank > highest))
                {
                    highest = card.Rank;
                }
            }

            return highest;
        }
    }

    public void Add(TarotPlayer player, TarotCard card) => Plays.Add((player, card));

    /// <summary>
    /// The winner of a completed trick: highest trump if any trump was played, otherwise the
    /// highest card of the lead suit. The Excuse never wins.
    /// </summary>
    public TarotPlayer DetermineWinner()
    {
        if (Plays.Count == 0)
        {
            return null;
        }

        var trumpPlays = Plays.Where(play => play.Card.IsTrump).ToList();
        if (trumpPlays.Count > 0)
        {
            return trumpPlays.MaxBy(play => play.Card.Rank).Player;
        }

        var leadSuit = LeadSuit;
        if (leadSuit is null)
        {
            return Plays[0].Player;
        }

        return Plays
            .Where(play => play.Card.Suit == leadSuit)
            .MaxBy(play => play.Card.Rank)
            .Player;
    }
}
