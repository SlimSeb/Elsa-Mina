using System.Globalization;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// A single French Tarot card. Suit cards use <see cref="Rank"/> 1-14 (11 = Jack, 12 = Cavalier,
/// 13 = Queen, 14 = King), trumps use 1-21, and the Excuse uses rank 0.
/// Point values are stored as integer half-points (value × 2) to avoid floating point drift.
/// </summary>
public sealed record TarotCard(TarotSuit Suit, int Rank)
{
    public const int JACK = 11;
    public const int CAVALIER = 12;
    public const int QUEEN = 13;
    public const int KING = 14;

    public const int PETIT = 1;
    public const int MONDE = 21;

    public bool IsTrump => Suit == TarotSuit.Trump;
    public bool IsExcuse => Suit == TarotSuit.Excuse;
    public bool IsKing => !IsTrump && !IsExcuse && Rank == KING;
    public bool IsQueen => !IsTrump && !IsExcuse && Rank == QUEEN;

    /// <summary>
    /// The three oudlers (bouts): the Petit (trump 1), the Monde (trump 21) and the Excuse.
    /// </summary>
    public bool IsOudler => IsExcuse || (IsTrump && Rank is PETIT or MONDE);

    /// <summary>
    /// Card value expressed in half-points (real value × 2): oudlers and kings = 9 (4.5),
    /// queens = 7 (3.5), cavaliers = 5 (2.5), jacks = 3 (1.5), everything else = 1 (0.5).
    /// </summary>
    public int HalfPoints
    {
        get
        {
            if (IsOudler)
            {
                return 9;
            }

            if (IsTrump)
            {
                return 1;
            }

            return Rank switch
            {
                KING => 9,
                QUEEN => 7,
                CAVALIER => 5,
                JACK => 3,
                _ => 1
            };
        }
    }

    public static TarotCard Parse(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token.Trim().ToLowerInvariant().Replace(" ", string.Empty);

        switch (normalized)
        {
            case "exc" or "excuse" or "x" or "fool":
                return new TarotCard(TarotSuit.Excuse, 0);
            case "petit":
                return new TarotCard(TarotSuit.Trump, PETIT);
            case "monde" or "world":
                return new TarotCard(TarotSuit.Trump, MONDE);
        }

        if (normalized[0] == 't')
        {
            if (int.TryParse(normalized[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trumpRank)
                && trumpRank is >= 1 and <= 21)
            {
                return new TarotCard(TarotSuit.Trump, trumpRank);
            }

            return null;
        }

        var suit = normalized[^1] switch
        {
            'h' => TarotSuit.Hearts,
            's' => TarotSuit.Spades,
            'd' => TarotSuit.Diamonds,
            'c' => TarotSuit.Clubs,
            _ => (TarotSuit?)null
        };

        if (suit is null)
        {
            return null;
        }

        var rankToken = normalized[..^1];
        var rank = rankToken switch
        {
            "j" => JACK,
            "c" => CAVALIER,
            "q" => QUEEN,
            "k" => KING,
            _ => int.TryParse(rankToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                 && value is >= 1 and <= 10
                ? value
                : 0
        };

        return rank == 0 ? null : new TarotCard(suit.Value, rank);
    }

    /// <summary>
    /// Canonical lowercase token that <see cref="Parse"/> round-trips (used in button values).
    /// </summary>
    public string ToToken()
    {
        if (IsExcuse)
        {
            return "exc";
        }

        if (IsTrump)
        {
            return $"t{Rank}";
        }

        var suitLetter = Suit switch
        {
            TarotSuit.Hearts => "h",
            TarotSuit.Spades => "s",
            TarotSuit.Diamonds => "d",
            _ => "c"
        };

        return $"{RankToken()}{suitLetter}";
    }

    /// <summary>
    /// Human-readable display with suit emoji, e.g. "K♥", "T21", "🃏". When <paramref name="culture"/>
    /// is French, trumps use "A" (Atout) and face cards use V/C/D/R (Valet, Cavalier, Dame, Roi).
    /// </summary>
    public string ToDisplay(CultureInfo culture = null)
    {
        if (IsExcuse)
        {
            return "🃏";
        }

        var isFrench = culture?.TwoLetterISOLanguageName == "fr";

        if (IsTrump)
        {
            return $"{(isFrench ? "A" : "T")}{Rank}";
        }

        var suitSymbol = Suit switch
        {
            TarotSuit.Hearts => "♥",
            TarotSuit.Spades => "♠",
            TarotSuit.Diamonds => "♦",
            _ => "♣"
        };

        return $"{DisplayRankToken(isFrench)}{suitSymbol}";
    }

    /// <summary>
    /// Canonical lowercase rank token that <see cref="Parse"/> round-trips (used in button values).
    /// </summary>
    private string RankToken() => Rank switch
    {
        JACK => "j",
        CAVALIER => "c",
        QUEEN => "q",
        KING => "k",
        _ => Rank.ToString(CultureInfo.InvariantCulture)
    };

    private string DisplayRankToken(bool isFrench) => Rank switch
    {
        JACK => isFrench ? "V" : "J",
        CAVALIER => "C",
        QUEEN => isFrench ? "D" : "Q",
        KING => isFrench ? "R" : "K",
        _ => Rank.ToString(CultureInfo.InvariantCulture)
    };

    public override string ToString() => ToDisplay();
}
