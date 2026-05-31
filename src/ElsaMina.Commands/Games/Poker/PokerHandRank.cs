namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// The category of a five-card poker hand, ordered from weakest to strongest so the enum value
/// itself can be compared.
/// </summary>
public enum PokerHandRank
{
    HighCard = 0,
    Pair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8
}
