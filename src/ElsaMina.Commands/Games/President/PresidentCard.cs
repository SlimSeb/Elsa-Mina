using System.Globalization;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// A single card of the 52-card deck used for Président. <see cref="Rank"/> uses 3-15 where
/// 11 = Jack, 12 = Queen, 13 = King, 14 = Ace and 15 = the 2, the strongest card of the game
/// (3 &lt; 4 &lt; ... &lt; K &lt; A &lt; 2).
/// </summary>
public sealed record PresidentCard(PresidentSuit Suit, int Rank)
{
    public const int JACK = 11;
    public const int QUEEN = 12;
    public const int KING = 13;
    public const int ACE = 14;
    public const int TWO = 15;

    public bool IsTwo => Rank == TWO;

    public static PresidentCard Parse(string token)
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
            'h' => PresidentSuit.Hearts,
            's' => PresidentSuit.Spades,
            'd' => PresidentSuit.Diamonds,
            'c' => PresidentSuit.Clubs,
            _ => (PresidentSuit?)null
        };

        if (suit is null)
        {
            return null;
        }

        var rank = ParseRank(normalized[..^1]);
        return rank is null ? null : new PresidentCard(suit.Value, rank.Value);
    }

    /// <summary>
    /// Parses a rank token on its own ("3"-"10", "t", "j", "q", "k", "a", "2") into its 3-15 value.
    /// </summary>
    public static int? ParseRank(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().ToLowerInvariant() switch
        {
            "t" => 10,
            "j" => JACK,
            "q" => QUEEN,
            "k" => KING,
            "a" or "1" => ACE,
            "2" => TWO,
            var digits => int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                          && value is >= 3 and <= 10
                ? value
                : null
        };
    }

    /// <summary>
    /// Canonical lowercase token that <see cref="Parse"/> round-trips (used in button values).
    /// </summary>
    public string ToToken()
    {
        var suitLetter = Suit switch
        {
            PresidentSuit.Hearts => "h",
            PresidentSuit.Spades => "s",
            PresidentSuit.Diamonds => "d",
            _ => "c"
        };

        return $"{RankToken(Rank)}{suitLetter}";
    }

    /// <summary>
    /// Canonical lowercase rank token that <see cref="ParseRank"/> round-trips (used in button values).
    /// </summary>
    public static string RankToken(int rank) => rank switch
    {
        JACK => "j",
        QUEEN => "q",
        KING => "k",
        ACE => "a",
        TWO => "2",
        _ => rank.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Human-readable display with suit symbol, e.g. "K♥", "10♠", "2♦". When <paramref name="culture"/>
    /// is French, face cards use V/D/R (Valet, Dame, Roi).
    /// </summary>
    public string ToDisplay(CultureInfo culture = null)
    {
        var suitSymbol = Suit switch
        {
            PresidentSuit.Hearts => "♥",
            PresidentSuit.Spades => "♠",
            PresidentSuit.Diamonds => "♦",
            _ => "♣"
        };

        return $"{DisplayRank(Rank, culture)}{suitSymbol}";
    }

    /// <summary>
    /// Human-readable rank on its own, e.g. "K", "10", "2" (V/D/R in French).
    /// </summary>
    public static string DisplayRank(int rank, CultureInfo culture = null)
    {
        var isFrench = culture?.TwoLetterISOLanguageName == "fr";

        return rank switch
        {
            JACK => isFrench ? "V" : "J",
            QUEEN => isFrench ? "D" : "Q",
            KING => isFrench ? "R" : "K",
            ACE => "A",
            TWO => "2",
            _ => rank.ToString(CultureInfo.InvariantCulture)
        };
    }

    public override string ToString() => ToDisplay();
}
