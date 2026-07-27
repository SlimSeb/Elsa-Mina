namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Why a proposed play cannot go on the pile, or <see cref="None"/> when it can. The game turns each
/// reason into its own message, so they are kept apart rather than collapsed into a single boolean.
/// </summary>
public enum PresidentPlayRejection
{
    None,

    /// <summary>
    /// The pile was opened with a different number of cards.
    /// </summary>
    WrongCount,

    /// <summary>
    /// The rank does not reach the top of the pile.
    /// </summary>
    TooLow,

    /// <summary>
    /// The "ou rien" rule pins the player to the pile's exact rank.
    /// </summary>
    MustMatch
}
