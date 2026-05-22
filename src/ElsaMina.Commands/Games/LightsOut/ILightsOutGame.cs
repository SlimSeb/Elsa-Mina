using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.LightsOut;

public interface ILightsOutGame : IGame
{
    int Level { get; }
    int GridSize { get; }
    int MoveCount { get; }
    int Stars { get; }
    int TotalStars { get; }
    bool IsRoundActive { get; }
    bool[,] Grid { get; }
    bool IsPrivateMode { get; }
    string TargetRoomId { get; }
    string TargetUserId { get; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task DisplayAnnounce();
    Task StartNewRound();
    Task ToggleCell(IUser user, int row, int col);
    Task CancelAsync();
}
