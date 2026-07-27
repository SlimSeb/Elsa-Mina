using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.President;

public static class PresidentConstants
{
    public const int MIN_PLAYERS = 3;
    public const int MAX_PLAYERS = 8;

    public const int DEFAULT_ROUNDS = 3;
    public const int MAX_ROUNDS = 10;

    /// <summary>
    /// Number of cards the scum owes the president (and gets back) during the exchange.
    /// </summary>
    public const int SCUM_EXCHANGE_COUNT = 2;

    /// <summary>
    /// Number of cards the vice-scum owes the vice-president (and gets back) during the exchange.
    /// </summary>
    public const int VICE_EXCHANGE_COUNT = 1;

    /// <summary>
    /// Minimum player count for the vice-president / vice-scum roles (and their exchange) to exist.
    /// </summary>
    public const int VICE_ROLES_MIN_PLAYERS = 4;

    public static readonly TimeSpan TURN_TIMEOUT = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How much time must remain on a player's turn when they are warned by PM that they risk timing out.
    /// </summary>
    public static readonly TimeSpan TURN_TIMEOUT_WARNING_REMAINING = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Builds a full, ordered 52-card deck (ranks 3 to 2, see <see cref="PresidentCard"/>).
    /// </summary>
    public static List<PresidentCard> BuildDeck()
    {
        var deck = new List<PresidentCard>(52);

        foreach (var suit in new[]
                 {
                     Suit.Hearts, Suit.Spades, Suit.Diamonds, Suit.Clubs
                 })
        {
            for (var rank = 3; rank <= PresidentCard.TWO; rank++)
            {
                deck.Add(new PresidentCard(suit, rank));
            }
        }

        return deck;
    }
}
