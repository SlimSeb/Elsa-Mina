using System.Collections.Immutable;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public class EloProgressionManager : IEloProgressionManager
{
    private readonly Lock _lock = new();
    private readonly HashSet<EloTrackedUser> _trackedUsers = [];

    public IReadOnlyCollection<EloTrackedUser> GetAllTrackedUsers()
    {
        lock (_lock)
        {
            return _trackedUsers.ToImmutableHashSet();
        }
    }

    public bool TrackUser(string format, string userId)
    {
        lock (_lock)
        {
            return _trackedUsers.Add(new EloTrackedUser(format, userId));
        }
    }

    public bool UntrackUser(string format, string userId)
    {
        lock (_lock)
        {
            return _trackedUsers.Remove(new EloTrackedUser(format, userId));
        }
    }
}
