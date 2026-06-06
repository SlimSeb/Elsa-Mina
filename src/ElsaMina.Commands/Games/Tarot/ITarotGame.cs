using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

public interface ITarotGame : IGame
{
    IContext Context { get; set; }

    IReadOnlyList<TarotPlayer> Players { get; }
    int PlayerCount { get; }
    TarotPhase Phase { get; }
    TarotPlayer CurrentPlayer { get; }
    TarotPlayer Taker { get; }
    TarotBid HighestBid { get; }

    IReadOnlyList<TarotCard> Dog { get; }
    IReadOnlyList<TarotCard> PendingDiscards { get; }
    bool DogRevealed { get; }
    TarotCard CalledKing { get; }
    TarotPlayer Partner { get; }
    bool PartnerRevealed { get; }

    TarotTrick CurrentTrick { get; }
    TarotTrick LastTrick { get; }
    TarotPlayer LastTrickWinner { get; }
    TarotCard LastPlayedCard { get; }
    int TrickNumber { get; }
    int TotalTricks { get; }

    TarotScoreResult ScoreResult { get; }

    Task BeginJoinPhaseAsync();
    Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user);
    Task StartAsync(IUser user);
    Task BidAsync(IUser user, TarotBid bid);
    Task CallKingAsync(IUser user, TarotCard card);
    Task DiscardAsync(IUser user, IReadOnlyList<TarotCard> cards);
    Task PlayAsync(IUser user, TarotCard card);
    Task ResendPlayerPageAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user);
    Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user, string targetPlayerId);
    void Cancel();
}
