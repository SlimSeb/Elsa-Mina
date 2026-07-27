namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// What a trick needs to know about the cards played into it. Everything else about resolving a trick
/// is the same across trick-taking games, so this is the only thing they have to supply.
/// </summary>
/// <typeparam name="TCard">The game's own card type.</typeparam>
public interface ITrickRules<in TCard>
{
    /// <summary>
    /// Whether the card beats every non-trump in the trick, whatever was led.
    /// </summary>
    bool IsTrump(TCard card);

    /// <summary>
    /// How strongly the card competes against others of the same category. Higher wins.
    /// </summary>
    int Strength(TCard card);

    /// <summary>
    /// Whether the card fixes the suit that must be followed. False only for tarot's Excuse, which can
    /// be thrown into any trick without committing its owner to anything.
    /// </summary>
    bool CountsForLead(TCard card);

    /// <summary>
    /// The suit that has to be followed to answer this card, or <c>null</c> when it has none (a tarot
    /// trump or the Excuse).
    /// </summary>
    Suit? SuitOf(TCard card);
}
