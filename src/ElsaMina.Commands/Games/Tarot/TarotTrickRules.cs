using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// How a tarot trick is resolved: trumps beat everything, cards compete on their rank, and the Excuse
/// neither fixes the suit to follow nor ever wins.
/// </summary>
public sealed class TarotTrickRules : ITrickRules<TarotCard>
{
    public static TarotTrickRules Instance { get; } = new();

    private TarotTrickRules()
    {
    }

    public bool IsTrump(TarotCard card) => card.IsTrump;

    public int Strength(TarotCard card) => card.Rank;

    public bool CountsForLead(TarotCard card) => !card.IsExcuse;

    public Suit? SuitOf(TarotCard card) => card.Suit;
}
