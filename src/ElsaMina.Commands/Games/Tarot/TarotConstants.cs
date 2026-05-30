namespace ElsaMina.Commands.Games.Tarot;

public static class TarotConstants
{
    public const int MIN_PLAYERS = 3;
    public const int MAX_PLAYERS = 5;

    public static readonly TimeSpan TURN_TIMEOUT = TimeSpan.FromSeconds(90);

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
