using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Catalog;

/// <summary>
/// Describes a game shown by the <c>-games</c> command.
/// </summary>
/// <param name="NameKey">Localization key for the game's display name.</param>
/// <param name="Command">Main command used to start the game (without the trigger).</param>
/// <param name="RequiredRank">Rank required to start the game.</param>
/// <param name="Mode">Whether the game is solo, multiplayer or room-wide.</param>
/// <param name="IsPlayableInPrivate">Whether the game can be played privately in PM.</param>
/// <param name="LeaderboardCommand">Leaderboard command (without the trigger), or null if none.</param>
public record GameInfo(
    string NameKey,
    string Command,
    Rank RequiredRank,
    GameMode Mode,
    bool IsPlayableInPrivate,
    string LeaderboardCommand = null);
