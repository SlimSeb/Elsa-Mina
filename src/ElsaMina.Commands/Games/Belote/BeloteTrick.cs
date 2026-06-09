namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// A single trick: the cards played by each player in order, with the logic to find the winner given
/// the trump suit.
/// </summary>
public sealed class BeloteTrick
{
    public BeloteTrick(BeloteSuit trump)
    {
        Trump = trump;
    }

    public BeloteSuit Trump { get; }

    public List<(BelotePlayer Player, BeloteCard Card)> Plays { get; } = [];

    public bool IsEmpty => Plays.Count == 0;

    /// <summary>
    /// The suit that must be followed: the suit of the first card played.
    /// </summary>
    public BeloteSuit? LeadSuit => Plays.Count > 0 ? Plays[0].Card.Suit : null;

    /// <summary>
    /// The strongest trump currently in the trick, or <c>null</c> if none has been played.
    /// </summary>
    public int? HighestTrumpStrength
    {
        get
        {
            int? highest = null;
            foreach (var (_, card) in Plays)
            {
                if (card.IsTrump(Trump))
                {
                    var strength = card.GetStrength(Trump);
                    if (highest is null || strength > highest)
                    {
                        highest = strength;
                    }
                }
            }

            return highest;
        }
    }

    public void Add(BelotePlayer player, BeloteCard card) => Plays.Add((player, card));

    /// <summary>
    /// The player currently winning the trick: highest trump if any trump was played, otherwise the
    /// highest card of the lead suit.
    /// </summary>
    public BelotePlayer CurrentWinner
    {
        get
        {
            if (Plays.Count == 0)
            {
                return null;
            }

            var trumpPlays = Plays.Where(play => play.Card.IsTrump(Trump)).ToList();
            if (trumpPlays.Count > 0)
            {
                return trumpPlays.MaxBy(play => play.Card.GetStrength(Trump)).Player;
            }

            var leadSuit = LeadSuit;
            return Plays
                .Where(play => play.Card.Suit == leadSuit)
                .MaxBy(play => play.Card.GetStrength(Trump))
                .Player;
        }
    }

    public BelotePlayer DetermineWinner() => CurrentWinner;
}
