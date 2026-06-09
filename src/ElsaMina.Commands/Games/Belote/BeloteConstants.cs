namespace ElsaMina.Commands.Games.Belote;

public static class BeloteConstants
{
    public const int PLAYER_COUNT = 4;
    public const int HAND_SIZE = 8;
    public const int TRICK_COUNT = 8;

    /// <summary>
    /// Card points the taker side must strictly exceed to make their contract (half of the 162 points
    /// available counting the last-trick bonus).
    /// </summary>
    public const int HALF_POINTS = 81;

    /// <summary>
    /// Total card points in the deck, before the last-trick bonus.
    /// </summary>
    public const int TOTAL_CARD_POINTS = 152;

    /// <summary>
    /// Bonus awarded to the team that wins the last trick ("dix de der").
    /// </summary>
    public const int LAST_TRICK_BONUS = 10;

    /// <summary>
    /// Bonus awarded to the team holding the King and Queen of trump ("belote et rebelote").
    /// </summary>
    public const int BELOTE_BONUS = 20;

    /// <summary>
    /// Total a team scores when winning every trick ("capot").
    /// </summary>
    public const int CAPOT_SCORE = 250;

    public static readonly TimeSpan TURN_TIMEOUT = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How much time must remain on a player's turn when they are warned by PM that they risk timing out.
    /// </summary>
    public static readonly TimeSpan TURN_TIMEOUT_WARNING_REMAINING = TimeSpan.FromSeconds(30);

    public static readonly IReadOnlyList<BeloteSuit> Suits =
    [
        BeloteSuit.Hearts,
        BeloteSuit.Spades,
        BeloteSuit.Diamonds,
        BeloteSuit.Clubs
    ];

    /// <summary>
    /// Builds a full, ordered 32-card Belote deck (7-10, Jack, Queen, King, Ace in four suits).
    /// </summary>
    public static List<BeloteCard> BuildDeck()
    {
        var deck = new List<BeloteCard>(32);
        foreach (var suit in Suits)
        {
            for (var rank = 7; rank <= BeloteCard.ACE; rank++)
            {
                deck.Add(new BeloteCard(suit, rank));
            }
        }

        return deck;
    }
}
