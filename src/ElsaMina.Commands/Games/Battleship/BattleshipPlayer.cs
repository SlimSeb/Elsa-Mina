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
}
