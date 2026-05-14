namespace ElsaMina.Commands.Games.Blackjack;

public class BlackjackHand
{
    public List<BlackjackCard> Cards { get; } = [];

    public int Value
    {
        get
        {
            var total = Cards.Sum(card => card.BlackjackValue);
            var aceCount = Cards.Count(card => card.Rank == 1);
            while (total > 21 && aceCount > 0)
            {
                total -= 10;
                aceCount--;
            }
            return total;
        }
    }

    public bool IsBust => Value > 21;
    public bool IsBlackjack => Cards.Count == 2 && Value == 21;

    public void Add(BlackjackCard card) => Cards.Add(card);
}
