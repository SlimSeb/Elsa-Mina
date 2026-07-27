using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A card game whose lobby lets a player give their seat back. Belote has no such command: its table
/// is either exactly full or not startable at all.
/// </summary>
public interface ILeavableCardGame : ICardGame
{
    Task<(bool Success, string MessageKey, object[] Args)> LeaveAsync(IUser user);
}
