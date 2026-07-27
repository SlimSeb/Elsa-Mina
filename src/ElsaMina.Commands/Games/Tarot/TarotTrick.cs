using ElsaMina.Commands.Games.Cards;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// A single tarot trick: the cards played by each player in order, with the logic to find the winner.
/// </summary>
public sealed class TarotTrick : Trick<TarotPlayer, TarotCard>
{
    public TarotTrick() : base(TarotTrickRules.Instance)
    {
    }

    /// <summary>
    /// The rank of the highest trump currently in the trick, or <c>null</c> if none has been played.
    /// Trumps compete on their rank, so their rank is their strength.
    /// </summary>
    public int? HighestTrumpRank => HighestTrumpStrength;
}
