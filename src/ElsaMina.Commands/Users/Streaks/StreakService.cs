using System.Collections.Concurrent;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Users.Streaks;

public class StreakService : IStreakService
{
    // Tracks (userId, roomId) pairs already updated on a given date to avoid redundant DB writes
    private readonly ConcurrentDictionary<(string UserId, string RoomId), DateOnly> _updatedToday = new();

    private readonly IBotDbContextFactory _dbContextFactory;

    public StreakService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task UpdateStreakAsync(string userId, string roomId, DateOnly activityDate,
        CancellationToken cancellationToken = default)
    {
        var key = (userId, roomId);
        if (_updatedToday.TryGetValue(key, out var cachedDate) && cachedDate == activityDate)
        {
            return;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var roomUser = await dbContext.RoomUsers
                .Where(ru => ru.Id == userId && ru.RoomId == roomId)
                .FirstOrDefaultAsync(cancellationToken);

            if (roomUser == null)
            {
                return;
            }

            if (roomUser.LastActivityDate == activityDate)
            {
                _updatedToday[key] = activityDate;
                return;
            }

            var yesterday = activityDate.AddDays(-1);
            if (roomUser.LastActivityDate == yesterday)
            {
                roomUser.CurrentStreak++;
            }
            else
            {
                roomUser.CurrentStreak = 1;
            }

            if (roomUser.CurrentStreak > roomUser.LongestStreak)
            {
                roomUser.LongestStreak = roomUser.CurrentStreak;
            }

            roomUser.LastActivityDate = activityDate;
            await dbContext.SaveChangesAsync(cancellationToken);
            _updatedToday[key] = activityDate;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update streak for user {0} in room {1}", userId, roomId);
        }
    }

    public async Task<(int CurrentStreak, int LongestStreak)> GetStreakAsync(string userId, string roomId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var roomUser = await dbContext.RoomUsers
            .Where(ru => ru.Id == userId && ru.RoomId == roomId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (roomUser == null)
        {
            return (0, 0);
        }

        return (roomUser.CurrentStreak, roomUser.LongestStreak);
    }
}
