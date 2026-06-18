using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipPlayer
{
    public BattleshipPlayer(IUser user)
    {
        User = user;
    }

    public IUser User { get; }
    public BattleshipBoard Board { get; } = new();

    /// <summary>
    /// Index of the next ship to place from <see cref="BattleshipConstants.FLEET"/> during the placement phase.
    /// </summary>
    public int NextShipIndex { get; set; }

    /// <summary>
    /// Orientation used when placing the next ship: <c>true</c> places it rightwards, <c>false</c> downwards.
    /// </summary>
    public bool IsHorizontalPlacement { get; set; } = true;

    public bool HasPlacedAllShips => NextShipIndex >= BattleshipConstants.FLEET.Count;
}
