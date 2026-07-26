using System.Globalization;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// The naming shared by every deck: the suit letters that go into button payloads, the suit symbols
/// players read, and the face-card letters, which differ between French (valet, cavalier, dame, roi)
/// and English. Each card record keeps its own rank scale and point values; only the naming is here.
/// </summary>
/// <remarks>
/// The letters produced here end up in <c>&lt;button name="send" value="..."&gt;</c> payloads and in
/// stored data, so they are a wire format: they must round-trip and must not drift.
/// </remarks>
public static class CardToken
{
    /// <summary>
    /// The suit a canonical token ends with, or <c>null</c> when the letter names no suit.
    /// </summary>
    public static Suit? ParseSuitLetter(char letter) => letter switch
    {
        'h' => Suit.Hearts,
        's' => Suit.Spades,
        'd' => Suit.Diamonds,
        'c' => Suit.Clubs,
        _ => null
    };

    /// <summary>
    /// The suit named by a whole word, in either language, as players type it in chat.
    /// </summary>
    public static Suit? ParseSuitName(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().ToLowerInvariant() switch
        {
            "h" or "hearts" or "coeur" or "coeurs" or "cœur" or "cœurs" or "heart" => Suit.Hearts,
            "s" or "spades" or "pique" or "piques" or "spade" => Suit.Spades,
            "d" or "diamonds" or "carreau" or "carreaux" or "diamond" => Suit.Diamonds,
            "c" or "clubs" or "trefle" or "trefles" or "trèfle" or "trèfles" or "club" => Suit.Clubs,
            _ => null
        };
    }

    /// <summary>
    /// The canonical lowercase letter a card token ends with.
    /// </summary>
    public static string SuitLetter(Suit suit) => suit switch
    {
        Suit.Hearts => "h",
        Suit.Spades => "s",
        Suit.Diamonds => "d",
        _ => "c"
    };

    /// <summary>
    /// The symbol shown to players.
    /// </summary>
    public static string SuitSymbol(Suit suit) => suit switch
    {
        Suit.Hearts => "♥",
        Suit.Spades => "♠",
        Suit.Diamonds => "♦",
        _ => "♣"
    };

    public static bool IsRed(Suit suit) => suit is Suit.Hearts or Suit.Diamonds;

    /// <summary>
    /// Whether the face cards should be labelled the French way.
    /// </summary>
    public static bool IsFrench(CultureInfo culture) => culture?.TwoLetterISOLanguageName == "fr";

    /// <summary>
    /// Valet or Jack.
    /// </summary>
    public static string Jack(bool isFrench) => isFrench ? "V" : "J";

    /// <summary>
    /// The cavalier (knight) sits between the jack and the queen in a tarot deck and is a C either way.
    /// </summary>
    public const string CAVALIER = "C";

    /// <summary>
    /// Dame or Queen.
    /// </summary>
    public static string Queen(bool isFrench) => isFrench ? "D" : "Q";

    /// <summary>
    /// Roi or King.
    /// </summary>
    public static string King(bool isFrench) => isFrench ? "R" : "K";

    /// <summary>
    /// The ace reads the same in both languages.
    /// </summary>
    public const string ACE = "A";

    /// <summary>
    /// The prefix a tarot trump is displayed with: atout in French, trump in English.
    /// </summary>
    public static string TrumpPrefix(bool isFrench) => isFrench ? "A" : "T";

    /// <summary>
    /// A plain numeric rank, formatted invariantly so it never picks up a culture's digits.
    /// </summary>
    public static string Number(int rank) => rank.ToString(CultureInfo.InvariantCulture);
}
