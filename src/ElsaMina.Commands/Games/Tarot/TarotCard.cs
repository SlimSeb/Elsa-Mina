using System.Globalization;
using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// A single French Tarot card. Suit cards use <see cref="Rank"/> 1-14 (11 = Jack, 12 = Cavalier,
/// 13 = Queen, 14 = King), trumps use 1-21, and the Excuse uses rank 0. Trumps and the Excuse belong
/// to no suit, so their <see cref="Suit"/> is <c>null</c>.
/// Point values are stored as integer half-points (value × 2) to avoid floating point drift.
/// </summary>
public sealed record TarotCard(TarotCardKind Kind, Suit? Suit, int Rank)
{
    public const int JACK = 11;
    public const int CAVALIER = 12;
    public const int QUEEN = 13;
    public const int KING = 14;

    public const int PETIT = 1;
    public const int MONDE = 21;

    /// <summary>
    /// A card of one of the four suits.
    /// </summary>
    public static TarotCard Suited(Suit suit, int rank) => new(TarotCardKind.Suited, suit, rank);

    /// <summary>
    /// A trump (atout) of the given rank, 1 to 21.
    /// </summary>
    public static TarotCard Trump(int rank) => new(TarotCardKind.Trump, null, rank);

    /// <summary>
    /// The Excuse (the fool), which belongs to no suit and has no rank.
    /// </summary>
    public static TarotCard Excuse { get; } = new(TarotCardKind.Excuse, null, 0);

    public bool IsTrump => Kind == TarotCardKind.Trump;
    public bool IsExcuse => Kind == TarotCardKind.Excuse;
    public bool IsKing => Kind == TarotCardKind.Suited && Rank == KING;
    public bool IsQueen => Kind == TarotCardKind.Suited && Rank == QUEEN;

    /// <summary>
    /// A face card (tête): a suit Jack, Cavalier, Queen or King. Trumps and the Excuse are never faces.
    /// </summary>
    public bool IsFaceCard => Kind == TarotCardKind.Suited && Rank >= JACK;

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

        var special = ParseSpecialCard(normalized);
        if (special != null)
        {
            return special;
        }

        return normalized[0] == 't' ? ParseTrumpCard(normalized) : ParseSuitCard(normalized);
    }

    private static TarotCard ParseSpecialCard(string normalized) => normalized switch
    {
        "exc" or "excuse" or "x" or "fool" => Excuse,
        "petit" => Trump(PETIT),
        "monde" or "world" => Trump(MONDE),
        _ => null
    };

    private static TarotCard ParseTrumpCard(string normalized)
    {
        if (int.TryParse(normalized[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trumpRank)
            && trumpRank is >= 1 and <= 21)
        {
            return Trump(trumpRank);
        }

        return null;
    }

    private static TarotCard ParseSuitCard(string normalized)
    {
        var suit = CardToken.ParseSuitLetter(normalized[^1]);
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

        return rank == 0 ? null : Suited(suit.Value, rank);
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

        return $"{RankToken()}{CardToken.SuitLetter(Suit!.Value)}";
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

        var isFrench = CardToken.IsFrench(culture);

        if (IsTrump)
        {
            return $"{CardToken.TrumpPrefix(isFrench)}{Rank}";
        }

        return $"{DisplayRankToken(isFrench)}{CardToken.SuitSymbol(Suit!.Value)}";
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
        _ => CardToken.Number(Rank)
    };

    private string DisplayRankToken(bool isFrench) => Rank switch
    {
        JACK => CardToken.Jack(isFrench),
        CAVALIER => CardToken.CAVALIER,
        QUEEN => CardToken.Queen(isFrench),
        KING => CardToken.King(isFrench),
        _ => CardToken.Number(Rank)
    };

    public override string ToString() => ToDisplay();
}
