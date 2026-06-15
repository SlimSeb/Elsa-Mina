namespace ElsaMina.Commands.Games.Tarot;

public static class TarotConstants
{
    public const int MIN_PLAYERS = 3;
    public const int MAX_PLAYERS = 5;

    public static readonly TimeSpan TURN_TIMEOUT = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How much time must remain on a player's turn when they are warned by PM that they risk timing out.
    /// </summary>
    public static readonly TimeSpan TURN_TIMEOUT_WARNING_REMAINING = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of cards dealt to each player, keyed by player count.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> HAND_SIZE = new Dictionary<int, int>
    {
        [3] = 24,
        [4] = 18,
        [5] = 15
    };

    /// <summary>
    /// Number of cards in the dog (chien), keyed by player count.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> DOG_SIZE = new Dictionary<int, int>
    {
        [3] = 6,
        [4] = 6,
        [5] = 3
    };

    /// <summary>
    /// Points the taker must reach to win, in half-points, keyed by the number of oudlers held.
    /// (56, 51, 41, 36 points respectively.)
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> TARGET_HALF_POINTS = new Dictionary<int, int>
    {
        [0] = 112,
        [1] = 102,
        [2] = 82,
        [3] = 72
    };

    public static readonly IReadOnlyDictionary<TarotBid, int> BID_MULTIPLIER = new Dictionary<TarotBid, int>
    {
        [TarotBid.Petite] = 1,
        [TarotBid.Garde] = 2,
        [TarotBid.GardeSans] = 4,
        [TarotBid.GardeContre] = 6
    };

    /// <summary>
    /// Bonus, in half-points (10 points × 2), awarded to the side that wins the Petit in the last trick.
    /// Added before the contract multiplier is applied.
    /// </summary>
    public const int PETIT_AU_BOUT_HALF_POINTS = 20;

    /// <summary>
    /// Minimum number of trumps a player must hold to declare a poignée (handful), keyed by player count.
    /// Index 0 = single, 1 = double, 2 = triple.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int[]> POIGNEE_THRESHOLDS = new Dictionary<int, int[]>
    {
        [3] = [13, 15, 18],
        [4] = [10, 13, 15],
        [5] = [8, 10, 13]
    };

    /// <summary>
    /// Flat poignée bonus in half-points (20/30/40 points × 2) for single/double/triple. Index 0 is unused.
    /// Awarded to the side that wins the deal, regardless of which side declared it.
    /// </summary>
    public static readonly int[] POIGNEE_HALF_POINTS = [0, 40, 60, 80];

    /// <summary>
    /// Chelem (slam) bonus in half-points (× 2): announced &amp; achieved, achieved without announcing,
    /// and announced but failed.
    /// </summary>
    public const int SLAM_ANNOUNCED_HALF_POINTS = 800;
    public const int SLAM_UNANNOUNCED_HALF_POINTS = 400;
    public const int SLAM_FAILED_HALF_POINTS = 400;

    /// <summary>
    /// Builds a full, ordered 78-card French Tarot deck (56 suit cards + 21 trumps + the Excuse).
    /// </summary>
    public static List<TarotCard> BuildDeck()
    {
        var deck = new List<TarotCard>(78);

        foreach (var suit in new[] { TarotSuit.Hearts, TarotSuit.Spades, TarotSuit.Diamonds, TarotSuit.Clubs })
        {
            for (var rank = 1; rank <= 14; rank++)
            {
                deck.Add(new TarotCard(suit, rank));
            }
        }

        for (var rank = 1; rank <= 21; rank++)
        {
            deck.Add(new TarotCard(TarotSuit.Trump, rank));
        }

        deck.Add(new TarotCard(TarotSuit.Excuse, 0));

        return deck;
    }
}
