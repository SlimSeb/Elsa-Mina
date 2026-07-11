using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

public interface IPresidentGame : IGame
{
    IReadOnlyList<PresidentPlayer> Players { get; }
    int PlayerCount { get; }
    PresidentPhase Phase { get; }
    PresidentPlayer CurrentPlayer { get; }
    int RoundNumber { get; }
    int TotalRounds { get; set; }

    PresidentTrick CurrentTrick { get; }
    PresidentPlayer LastTrickWinner { get; }
    IReadOnlyList<PresidentPlayer> FinishOrder { get; }

    IReadOnlyList<string> Log { get; }

    IReadOnlyList<(int Rank, int Count)> GetLegalPlays(PresidentPlayer player);
    bool CanPass(PresidentPlayer player);

    Task BeginJoinPhaseAsync();
    Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> LeaveAsync(IUser user);
    Task StartAsync(IUser user);
    Task PlayAsync(IUser user, int rank, int count);
    Task PassAsync(IUser user);
    Task GiveAsync(IUser user, IReadOnlyList<PresidentCard> cards);
    Task ResendPlayerPageAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user, string targetPlayerId);
    Task CancelAsync();
}
