namespace ElsaMina.Commands.Games.Blackjack;

public record BlackjackCard(int Rank, string Suit)
{
    public string DisplayRank => Rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => Rank.ToString()
    };

    public int BlackjackValue => Rank switch
    {
        1 => 11,
        >= 10 => 10,
        _ => Rank
    };

    public bool IsRed => Suit is "♥" or "♦";
}
