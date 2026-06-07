namespace ElsaMina.Commands.Games.Tcg.Decks;

public static class TcgDeckConstants
{
    /// <summary>Number of cards a legal deck must contain.</summary>
    public const int DECK_SIZE = 20;

    /// <summary>Maximum copies of a single card id allowed in a deck.</summary>
    public const int MAX_COPIES = 2;

    public const int MIN_ENERGY_TYPES = 1;
    public const int MAX_ENERGY_TYPES = 3;

    /// <summary>Maximum number of decks a single user may save.</summary>
    public const int MAX_DECKS_PER_USER = 12;

    public const int MAX_NAME_LENGTH = 30;
}
