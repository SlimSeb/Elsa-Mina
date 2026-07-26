using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// How a belote trick is resolved against a given trump suit. Every card fixes the suit to follow, and
/// strength depends on whether the card is trump: under trump the jack and the nine jump to the top.
/// </summary>
public sealed class BeloteTrickRules : ITrickRules<BeloteCard>
{
    private readonly Suit _trump;

    public BeloteTrickRules(Suit trump)
    {
        _trump = trump;
    }

    public bool IsTrump(BeloteCard card) => card.IsTrump(_trump);

    public int Strength(BeloteCard card) => card.GetStrength(_trump);

    public bool CountsForLead(BeloteCard card) => true;

    public Suit? SuitOf(BeloteCard card) => card.Suit;
}
