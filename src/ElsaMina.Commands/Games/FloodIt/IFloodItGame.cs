using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.FloodIt;

public interface IFloodItGame : IGame
{
    int Level { get; }
    int GridSize { get; }
    int ColorCount { get; }
    int MoveCount { get; }
    int MaxMoves { get; }
    int Stars { get; }
    int TotalStars { get; }
    bool IsRoundActive { get; }
    int[,] Grid { get; }
    bool[,] IsClaimed { get; }
    bool IsPrivateMode { get; }
    string TargetRoomId { get; }
    string TargetUserId { get; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task DisplayAnnounce();
    Task StartNewRound();
    Task FloodFill(IUser user, int colorIndex);
    Task CancelAsync();
}
