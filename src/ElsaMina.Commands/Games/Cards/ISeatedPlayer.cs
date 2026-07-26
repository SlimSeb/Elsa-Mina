using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A seat at a card game table, whoever is currently sitting in it.
/// </summary>
public interface ISeatedPlayer
{
    IUser User { get; }
    string UserId { get; }
    string Name { get; }
}
