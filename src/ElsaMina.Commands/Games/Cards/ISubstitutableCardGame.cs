using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A card game whose seats can change hands mid-game: a player asks to be replaced, and someone who is
/// not playing takes their seat with its hand and history intact. Poker has no substitutions because a
/// seat there owns real bucks.
/// </summary>
public interface ISubstitutableCardGame : ICardGame
{
    Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user);

    Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user, string targetPlayerId);
}
