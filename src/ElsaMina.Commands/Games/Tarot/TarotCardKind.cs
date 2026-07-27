namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// What kind of card this is in a tarot deck. Trumps and the Excuse belong to no suit at all, so they
/// are a separate axis rather than extra members of the suit enum: that way a card of hearts can never
/// be confused with a trump by a plain suit comparison.
/// </summary>
/// <remarks>
/// Declaration order is the order hands are sorted in, so suited cards come first and the Excuse last.
/// </remarks>
public enum TarotCardKind
{
    Suited,
    Trump,
    Excuse
}
