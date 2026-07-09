using System.Collections.Concurrent;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Showdown.Ladder;

public class LadderHistoryManager : ILadderHistoryManager
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, int>> _ladderEntriesById =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, int>> _ladderPlacementsById =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string LadderId, string Prefix), IReadOnlyDictionary<string, int>>
        _prefixedPlacementsById = new();

    public IReadOnlyDictionary<string, int> GetPreviousEntriesAndSave(string ladderId,
        IEnumerable<LadderPlayerDto> players)
    {
        return GetPreviousEntriesAndSave(_ladderEntriesById, NormalizeKey(ladderId), players,
            player => (int)Math.Round(player.Elo, MidpointRounding.AwayFromZero));
    }

    public IReadOnlyDictionary<string, int> GetPreviousPlacementsAndSave(string ladderId,
        IEnumerable<LadderPlayerDto> players)
    {
        return GetPreviousEntriesAndSave(_ladderPlacementsById, NormalizeKey(ladderId), players, player => player.Index);
    }

    public IReadOnlyDictionary<string, int> GetPreviousPrefixedPlacementsAndSave(string ladderId, string prefix,
        IEnumerable<LadderPlayerDto> players)
    {
        return GetPreviousEntriesAndSave(_prefixedPlacementsById, (NormalizeKey(ladderId), NormalizeKey(prefix)), players,
            player => player.InnerIndex);
    }

    private static string NormalizeKey(string value)
    {
        return value?.ToLowerAlphaNum() ?? string.Empty;
    }

    private static IReadOnlyDictionary<string, int> GetPreviousEntriesAndSave<TKey>(
        ConcurrentDictionary<TKey, IReadOnlyDictionary<string, int>> store,
        TKey key,
        IEnumerable<LadderPlayerDto> players,
        Func<LadderPlayerDto, int> valueSelector) where TKey : notnull
    {
        var currentEntries = BuildEntriesByPlayer(players, valueSelector);

        while (true)
        {
            if (store.TryGetValue(key, out var previousEntries))
            {
                store[key] = currentEntries;
                return previousEntries;
            }

            if (store.TryAdd(key, currentEntries))
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }
        }
    }

    private static Dictionary<string, int> BuildEntriesByPlayer(IEnumerable<LadderPlayerDto> players,
        Func<LadderPlayerDto, int> valueSelector)
    {
        var entriesByPlayer = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var player in players)
        {
            var playerId = string.IsNullOrWhiteSpace(player.UserId) ? string.Empty : player.UserId.ToLowerAlphaNum();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                playerId = string.IsNullOrWhiteSpace(player.Username) ? string.Empty : player.Username.ToLowerAlphaNum();
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            entriesByPlayer[playerId] = valueSelector(player);
        }

        return entriesByPlayer;
    }
}
