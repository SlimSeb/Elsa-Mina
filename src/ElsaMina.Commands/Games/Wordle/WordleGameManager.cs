using System.Collections.Concurrent;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleGameManager : IWordleGameManager
{
    private readonly ConcurrentDictionary<(string RoomId, string UserId), IWordleGame> _games = new();

    public IWordleGame GetGame(string roomId, string userId)
    {
        _games.TryGetValue((roomId, userId), out var game);
        return game;
    }

    public void RegisterGame(string roomId, string userId, IWordleGame game)
    {
        var key = (roomId, userId);
        _games[key] = game;
        game.GameEnded += () => _games.TryRemove(key, out _);
    }

    public void RemoveGame(string roomId, string userId)
    {
        _games.TryRemove((roomId, userId), out _);
    }
}
