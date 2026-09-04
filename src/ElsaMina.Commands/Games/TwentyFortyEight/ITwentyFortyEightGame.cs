using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

public interface ITwentyFortyEightGame : ICancellableGame
{
    int Score { get; }
    int BestScore { get; }
    int Wins { get; }
    int[,] Grid { get; }
    bool IsRoundActive { get; }
    bool IsPrivateMode { get; set; }
    string TargetRoomId { get; set; }
    string TargetUserId { get; set; }
    IContext Context { get; set; }
    IUser Owner { get; set; }

    Task DisplayAnnounce();
    Task StartNewRound();
    Task MakeMove(IUser user, string direction);
}
