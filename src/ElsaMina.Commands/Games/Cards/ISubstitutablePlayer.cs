using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A seat that can change hands mid-game. The seat keeps its hand, its captured cards and its turn
/// position: only the user behind it changes.
/// </summary>
public interface ISubstitutablePlayer : ISeatedPlayer
{
    /// <summary>
    /// True when whoever holds this seat has asked to be replaced.
    /// </summary>
    bool WantsSub { get; set; }

    /// <summary>
    /// Hands this seat over to another user, keeping everything else intact.
    /// </summary>
    void SubstituteWith(IUser user);
}
