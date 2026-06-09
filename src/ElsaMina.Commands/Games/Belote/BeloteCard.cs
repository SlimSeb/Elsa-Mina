using System.Globalization;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// A single card from the 32-card Belote deck. <see cref="Rank"/> uses 7-10 for the number cards
/// and 11 = Jack, 12 = Queen, 13 = King, 14 = Ace. Card strength and point value depend on whether
/// the card belongs to the trump suit, so they are computed against the current trump rather than
/// being intrinsic to the card.
/// </summary>
public sealed record BeloteCard(BeloteSuit Suit, int Rank)
{
    public const int JACK = 11;
    public const int QUEEN = 12;
    public const int KING = 13;
    public const int ACE = 14;

    /// <summary>
    /// Point value of each rank when the card is trump (Jack and 9 are boosted).
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> TrumpPoints = new Dictionary<int, int>
    {
        [JACK] = 20,
        [9] = 14,
        [ACE] = 11,
        [10] = 10,
        [KING] = 4,
        [QUEEN] = 3,
        [8] = 0,
        [7] = 0
    };

    /// <summary>
    /// Point value of each rank when the card is a plain (non-trump) suit.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> PlainPoints = new Dictionary<int, int>
    {
        [ACE] = 11,
        [10] = 10,
        [KING] = 4,
        [QUEEN] = 3,
        [JACK] = 2,
        [9] = 0,
        [8] = 0,
        [7] = 0
    };

    /// <summary>
    /// Relative strength (higher beats lower) of each rank within the trump suit:
    /// J &gt; 9 &gt; A &gt; 10 &gt; K &gt; Q &gt; 8 &gt; 7.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> TrumpStrength = new Dictionary<int, int>
    {
        [JACK] = 8,
        [9] = 7,
        [ACE] = 6,
        [10] = 5,
        [KING] = 4,
        [QUEEN] = 3,
        [8] = 2,
        [7] = 1
    };

    /// <summary>
    /// Relative strength (higher beats lower) of each rank within a plain suit:
    /// A &gt; 10 &gt; K &gt; Q &gt; J &gt; 9 &gt; 8 &gt; 7.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> PlainStrength = new Dictionary<int, int>
    {
        [ACE] = 8,
        [10] = 7,
        [KING] = 6,
        [QUEEN] = 5,
        [JACK] = 4,
        [9] = 3,
        [8] = 2,
        [7] = 1
    };

    public bool IsTrump(BeloteSuit trump) => Suit == trump;

    public int GetPoints(BeloteSuit trump) => IsTrump(trump) ? TrumpPoints[Rank] : PlainPoints[Rank];

    public int GetStrength(BeloteSuit trump) => IsTrump(trump) ? TrumpStrength[Rank] : PlainStrength[Rank];

    public static BeloteCard Parse(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        if (normalized.Length < 2)
        {
            return null;
        }

        var suit = normalized[^1] switch
        {
            'h' => BeloteSuit.Hearts,
            's' => BeloteSuit.Spades,
            'd' => BeloteSuit.Diamonds,
            'c' => BeloteSuit.Clubs,
            _ => (BeloteSuit?)null
        };

        if (suit is null)
        {
            return null;
        }

        var rankToken = normalized[..^1];
        var rank = rankToken switch
        {
            "j" or "v" => JACK,
            "q" => QUEEN,
            "k" or "r" => KING,
            "a" or "1" => ACE,
            _ => int.TryParse(rankToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                 && value is >= 7 and <= 10
                ? value
                : 0
        };

        return rank == 0 ? null : new BeloteCard(suit.Value, rank);
    }

    public static BeloteSuit? ParseSuit(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().ToLowerInvariant() switch
        {
            "h" or "hearts" or "coeur" or "coeurs" or "cœur" or "cœurs" or "heart" => BeloteSuit.Hearts,
            "s" or "spades" or "pique" or "piques" or "spade" => BeloteSuit.Spades,
            "d" or "diamonds" or "carreau" or "carreaux" or "diamond" => BeloteSuit.Diamonds,
            "c" or "clubs" or "trefle" or "trefles" or "trèfle" or "trèfles" or "club" => BeloteSuit.Clubs,
            _ => null
        };
    }

    /// <summary>
    /// Canonical lowercase token that <see cref="Parse"/> round-trips (used in button values).
    /// </summary>
    public string ToToken() => $"{RankToken()}{SuitLetter()}";

    private string SuitLetter() => Suit switch
    {
        BeloteSuit.Hearts => "h",
        BeloteSuit.Spades => "s",
        BeloteSuit.Diamonds => "d",
        _ => "c"
    };

    private string RankToken() => Rank switch
    {
        JACK => "j",
        QUEEN => "q",
        KING => "k",
        ACE => "a",
        _ => Rank.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Human-readable display with suit emoji, e.g. "K♥", "10♠". When <paramref name="culture"/> is
    /// French, face cards use V/D/R (Valet, Dame, Roi) and the Ace stays "A".
    /// </summary>
    public string ToDisplay(CultureInfo culture = null)
    {
        var isFrench = culture?.TwoLetterISOLanguageName == "fr";
        var suitSymbol = Suit switch
        {
            BeloteSuit.Hearts => "♥",
            BeloteSuit.Spades => "♠",
            BeloteSuit.Diamonds => "♦",
            _ => "♣"
        };

        return $"{DisplayRankToken(isFrench)}{suitSymbol}";
    }

    private string DisplayRankToken(bool isFrench) => Rank switch
    {
        JACK => isFrench ? "V" : "J",
        QUEEN => isFrench ? "D" : "Q",
        KING => isFrench ? "R" : "K",
        ACE => "A",
        _ => Rank.ToString(CultureInfo.InvariantCulture)
    };

    public static string SuitDisplay(BeloteSuit suit) => suit switch
    {
        BeloteSuit.Hearts => "♥",
        BeloteSuit.Spades => "♠",
        BeloteSuit.Diamonds => "♦",
        _ => "♣"
    };

    public static string SuitToken(BeloteSuit suit) => suit switch
    {
        BeloteSuit.Hearts => "h",
        BeloteSuit.Spades => "s",
        BeloteSuit.Diamonds => "d",
        _ => "c"
    };

    public override string ToString() => ToDisplay();
}
