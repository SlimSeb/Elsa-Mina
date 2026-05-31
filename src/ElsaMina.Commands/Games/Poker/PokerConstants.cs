namespace ElsaMina.Commands.Games.Poker;

public static class PokerConstants
{
    public const int MIN_PLAYERS = 2;
    public const int MAX_PLAYERS = 9;

    public const long DEFAULT_BUY_IN = 100;
    public const long MIN_BUY_IN = 20;

    public static readonly TimeSpan TURN_TIMEOUT = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The big blind for a given buy-in: a twentieth of the stack, but never less than 2.
    /// </summary>
    public static long BigBlind(long buyIn) => Math.Max(2, buyIn / 20);

    /// <summary>
    /// The small blind for a given buy-in: half the big blind, but never less than 1.
    /// </summary>
    public static long SmallBlind(long buyIn) => Math.Max(1, BigBlind(buyIn) / 2);

    /// <summary>
    /// Builds a standard, ordered 52-card deck.
    /// </summary>
    public static List<PokerCard> BuildDeck()
    {
        var deck = new List<PokerCard>(52);
        foreach (var suit in new[] { PokerSuit.Clubs, PokerSuit.Diamonds, PokerSuit.Hearts, PokerSuit.Spades })
        {
            for (var rank = 2; rank <= PokerCard.ACE; rank++)
            {
                deck.Add(new PokerCard(suit, rank));
            }
        }

        return deck;
    }
}
