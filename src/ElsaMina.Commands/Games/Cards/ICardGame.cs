using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// What every seated card game (tarot, belote, président, poker) offers regardless of its rules: a
/// lobby that fills up, a start, and a way to be called off. The lifecycle commands are written
/// against this rather than against each game's own interface.
/// </summary>
public interface ICardGame : IGame
{
    IContext Context { get; set; }

    /// <summary>
    /// True while the game is still gathering players and no cards have been dealt.
    /// </summary>
    bool IsInLobby { get; }

    /// <summary>
    /// Whether the given user currently holds a seat.
    /// </summary>
    bool HasPlayer(string userId);

    /// <summary>
    /// Posts the lobby panel so players can join.
    /// </summary>
    Task BeginJoinPhaseAsync();

    Task<(bool Success, string MessageKey, object[] Args)> JoinAsync(IUser user);

    Task StartAsync(IUser user);

    Task CancelAsync();
}
