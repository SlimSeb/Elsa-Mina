using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

public interface IPokerGame : IGame
{
    IContext Context { get; set; }
    long BuyIn { get; set; }

    IReadOnlyList<PokerPlayer> Players { get; }
    int PlayerCount { get; }
    PokerPhase Phase { get; }
    PokerPlayer CurrentPlayer { get; }

    IReadOnlyList<PokerCard> CommunityCards { get; }

    long BigBlindAmount { get; }
    long SmallBlindAmount { get; }
    long CurrentBet { get; }
    long LastRaiseAmount { get; }

    /// <summary>
    /// Total chips committed to the pot across every player and betting round.
    /// </summary>
    long TotalPot { get; }

    PokerPlayer Dealer { get; }
    PokerPlayer SmallBlindPlayer { get; }
    PokerPlayer BigBlindPlayer { get; }

    /// <summary>
    /// The pots (main and side) resolved at showdown. Empty until the hand is over.
    /// </summary>
    IReadOnlyList<PokerPot> Pots { get; }

    /// <summary>
    /// True when at least two players reached showdown (as opposed to everyone else folding).
    /// </summary>
    bool WentToShowdown { get; }

    /// <summary>
    /// Chips the given player still has to put in to match the current bet.
    /// </summary>
    long AmountToCall(PokerPlayer player);

    /// <summary>
    /// The smallest legal total a player may raise to in the current round.
    /// </summary>
    long MinimumRaiseTo();

    Task BeginJoinPhaseAsync();
    Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user);
    Task StartAsync(IUser user);
    Task FoldAsync(IUser user);
    Task CheckAsync(IUser user);
    Task CallAsync(IUser user);
    Task RaiseAsync(IUser user, long amountTo);
    Task CancelAsync();
}
