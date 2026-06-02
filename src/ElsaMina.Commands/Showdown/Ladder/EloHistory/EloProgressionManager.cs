using System.Collections.Immutable;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public class EloProgressionManager : IEloProgressionManager
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly Lock _lock = new();
    private readonly HashSet<EloTrackedUser> _trackedUsers = [];

    public EloProgressionManager(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storedEntries = await dbContext.TrackedEloUsers.ToListAsync(cancellationToken);

        lock (_lock)
        {
            foreach (var entry in storedEntries)
            {
                _trackedUsers.Add(new EloTrackedUser(entry.Format, entry.UserId));
            }
        }
    }

    public IReadOnlyCollection<EloTrackedUser> GetAllTrackedUsers()
    {
        lock (_lock)
        {
            return _trackedUsers.ToImmutableHashSet();
        }
    }

    public async Task<bool> TrackUserAsync(string format, string userId, CancellationToken cancellationToken = default)
    {
        bool isNew;
        lock (_lock)
        {
            isNew = _trackedUsers.Add(new EloTrackedUser(format, userId));
        }

        if (!isNew)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureUserExistsAsync(userId, cancellationToken);
        await dbContext.TrackedEloUsers.AddAsync(new TrackedEloUser { Format = format, UserId = userId },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UntrackUserAsync(string format, string userId,
        CancellationToken cancellationToken = default)
    {
        bool wasRemoved;
        lock (_lock)
        {
            wasRemoved = _trackedUsers.Remove(new EloTrackedUser(format, userId));
        }

        if (!wasRemoved)
        {
            return false;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await dbContext.TrackedEloUsers.FindAsync([format, userId], cancellationToken);
        if (entry != null)
        {
            dbContext.TrackedEloUsers.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
