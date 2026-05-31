using System.Globalization;

namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// A single playing card. <see cref="Rank"/> goes from 2 to 14 (11 = Jack, 12 = Queen,
/// 13 = King, 14 = Ace).
/// </summary>
public sealed record PokerCard(PokerSuit Suit, int Rank)
{
    public const int JACK = 11;
    public const int QUEEN = 12;
    public const int KING = 13;
    public const int ACE = 14;

    public bool IsRed => Suit is PokerSuit.Hearts or PokerSuit.Diamonds;

    /// <summary>
    /// Short rank label, e.g. "A", "K", "Q", "J", "10", "2".
    /// </summary>
    public string RankLabel => Rank switch
    {
        ACE => "A",
        KING => "K",
        QUEEN => "Q",
        JACK => "J",
        _ => Rank.ToString(CultureInfo.InvariantCulture)
    };

    public string SuitSymbol => Suit switch
    {
        PokerSuit.Hearts => "♥",
        PokerSuit.Diamonds => "♦",
        PokerSuit.Spades => "♠",
        _ => "♣"
    };

    /// <summary>
    /// Human-readable display, e.g. "A♠", "10♥".
    /// </summary>
    public string ToDisplay() => $"{RankLabel}{SuitSymbol}";

    public override string ToString() => ToDisplay();
}
