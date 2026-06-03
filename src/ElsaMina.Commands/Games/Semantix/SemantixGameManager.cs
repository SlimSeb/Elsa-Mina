using System.Collections.Concurrent;

namespace ElsaMina.Commands.Games.Semantix;

public class SemantixGameManager : ISemantixGameManager
{
    private readonly ConcurrentDictionary<(string RoomId, string UserId), ISemantixGame> _games = new();

    public ISemantixGame GetGame(string roomId, string userId)
    {
        _games.TryGetValue((roomId, userId), out var game);
        return game;
    }

    public void RegisterGame(string roomId, string userId, ISemantixGame game)
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
