using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

public interface IChessGame : IGame
{
    ChessBoard Board { get; }
    IReadOnlyList<IUser> Players { get; }
    IUser WhitePlayer { get; }
    IUser BlackPlayer { get; }
    IUser PlayerCurrentlyPlaying { get; }
    int TurnCount { get; }
    int GameId { get; }
    string PlayerNames { get; }
    (int Row, int Column)? SelectedSquare { get; }
    IReadOnlyCollection<(int Row, int Column)> SelectedSquareDestinations { get; }
    IContext Context { get; set; }
    Task DisplayAnnounce();
    Task JoinGame(IUser user);
    Task Play(IUser user, string input);
    Task Forfeit(IUser user);
    void Cancel();
}
