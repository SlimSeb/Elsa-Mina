using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// A single belote trick: the cards played by each player in order, with the logic to find the winner
/// given the trump suit.
/// </summary>
public sealed class BeloteTrick : Trick<BelotePlayer, BeloteCard>
{
    public BeloteTrick(Suit trump) : base(new BeloteTrickRules(trump))
    {
        Trump = trump;
    }

    public Suit Trump { get; }
}
