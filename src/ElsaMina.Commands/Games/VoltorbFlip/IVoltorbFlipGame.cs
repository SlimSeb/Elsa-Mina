using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

public interface IVoltorbFlipGame : IGame
{
    int Level { get; }
    bool IsRoundActive { get; }
    int CurrentCoins { get; }
    int TotalCoins { get; }
    int[,] TileValues { get; }
    bool[,] IsRevealed { get; }
    int[] RowSums { get; }
    int[] ColSums { get; }
    int[] RowVoltorbs { get; }
    int[] ColVoltorbs { get; }
    bool IsMarkingMode { get; }
    VoltorbFlipMarkerType ActiveMarkerType { get; }
    VoltorbFlipMarkerType[,] Markers { get; }
    bool IsPrivateMode { get; }
    string TargetRoomId { get; }
    string TargetUserId { get; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task DisplayAnnounce();
    Task StartNewRound();
    Task FlipTile(IUser user, int row, int col);
    Task SetMarkerType(IUser user, VoltorbFlipMarkerType markerType);
    Task QuitRound(IUser user);
    Task CancelAsync();
}
