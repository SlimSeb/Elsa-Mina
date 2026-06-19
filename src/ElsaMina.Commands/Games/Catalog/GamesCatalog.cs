using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Catalog;

/// <summary>
/// Central list of all games exposed by the <c>-games</c> command.
/// When adding a new game, add a single entry here.
/// </summary>
public static class GamesCatalog
{
    public static readonly IReadOnlyList<GameInfo> Games =
    [
        // Solo games (each player plays their own board, scores/leaderboards)
        new("games_voltorbflip", "voltorbflip", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "vfl"),
        new("games_lightsout", "lightsout", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "lol"),
        new("games_2048", "2048", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "2048lb"),
        new("games_floodit", "floodit", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "fil"),
        new("games_wordle", "wordle", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "wll"),
        new("games_semantix", "semantix", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true, LeaderboardCommand: "sxl"),
        new("games_blackjack", "blackjack", Rank.Voiced, GameMode.Solo, IsPlayableInPrivate: true),

        // Multiplayer / competitive games
        new("games_tarot", "tarot", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false, LeaderboardCommand: "tarotlb"),
        new("games_belote", "belote", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false, LeaderboardCommand: "belotelb"),
        new("games_poker", "poker", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false),
        new("games_connectfour", "connectfour", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false, LeaderboardCommand: "c4lb"),
        new("games_chess", "chess", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false, LeaderboardCommand: "chesslb"),
        new("games_battleship", "battleship", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false, LeaderboardCommand: "bslb"),
        new("games_rps", "rps", Rank.Voiced, GameMode.Multiplayer, IsPlayableInPrivate: false),
        new("games_pokerace", "pokerace", Rank.Driver, GameMode.Multiplayer, IsPlayableInPrivate: false),

        // Room-wide guessing games (everyone in the room guesses)
        new("games_guessing", "guessinggame", Rank.Voiced, GameMode.RoomWide, IsPlayableInPrivate: false)
    ];
}
