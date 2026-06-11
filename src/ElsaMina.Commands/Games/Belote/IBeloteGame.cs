using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

public interface IBeloteGame : IGame
{
    IContext Context { get; set; }

    IReadOnlyList<BelotePlayer> Players { get; }
    int PlayerCount { get; }
    BelotePhase Phase { get; }
    int BiddingRound { get; }
    BelotePlayer CurrentPlayer { get; }
    BelotePlayer Taker { get; }
    BeloteCard TurnedCard { get; }
    BeloteSuit? Trump { get; }

    BeloteTrick CurrentTrick { get; }
    BeloteTrick LastTrick { get; }
    BelotePlayer LastTrickWinner { get; }
    BeloteCard LastPlayedCard { get; }
    int TrickNumber { get; }
    int TotalTricks { get; }

    int Team0Tricks { get; }
    int Team1Tricks { get; }

    BeloteScoreResult ScoreResult { get; }

    Task BeginJoinPhaseAsync();
    Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user);
    Task StartAsync(IUser user);
    Task BidAsync(IUser user, bool pass, BeloteSuit? chosenSuit);
    Task PlayAsync(IUser user, BeloteCard card);
    Task ResendPlayerPageAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user, string targetPlayerId);
    IReadOnlyCollection<BeloteCard> GetLegalMoves(BelotePlayer player);
    Task CancelAsync();
}
