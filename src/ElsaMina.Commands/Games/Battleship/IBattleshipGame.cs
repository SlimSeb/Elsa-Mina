using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

public interface IBattleshipGame : IGame
{
    IReadOnlyList<BattleshipPlayer> Players { get; }
    IUser PlayerCurrentlyPlaying { get; }
    int TurnCount { get; }
    int GameId { get; }
    string PlayerNames { get; }
    Task DisplayAnnounce();
    Task JoinGame(IUser user);
    Task Fire(IUser user, string coordinate);
    Task Forfeit(IUser user);
    void Cancel();
}
