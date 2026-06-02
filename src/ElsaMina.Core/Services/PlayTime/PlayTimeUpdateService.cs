using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Core.Services.PlayTime;

public class PlayTimeUpdateService : IPlayTimeUpdateService
{
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IUserSaveQueue _userSaveQueue;
    private readonly IRoomsManager _roomsManager;

    private readonly SemaphoreSlim _playTimeUpdateSemaphoreSlim = new(1, 1);
    private PeriodicTimerRunner _timerRunner;

    public PlayTimeUpdateService(
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory,
        IUserSaveQueue userSaveQueue,
        IRoomsManager roomsManager)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _userSaveQueue = userSaveQueue;
        _roomsManager = roomsManager;
    }

    public void Initialize()
    {
        _timerRunner = new PeriodicTimerRunner(_configuration.PlayTimeUpdatesInterval,
            ProcessPendingPlayTimeUpdatesAsync);
        _timerRunner.Start();
    }

    public async Task ProcessPendingPlayTimeUpdatesAsync()
    {
        await _playTimeUpdateSemaphoreSlim.WaitAsync();
        try
        {
            await UpdatePlayTimesAsync();
        }
        finally
        {
            _playTimeUpdateSemaphoreSlim.Release();
        }
    }

    // This is a mess
    private async Task UpdatePlayTimesAsync()
    {
        await _userSaveQueue.AcquireLockAsync();
        try
        {
            Log.Information("Processing play time updates...");

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            foreach (var room in _roomsManager.Rooms)
            {
                var roomId = room.RoomId;
                var updates = room.PendingPlayTimeUpdates.ToList();

                foreach (var (userId, playTime) in updates)
                {
                    try
                    {
                        await IncrementUserPlayTime(dbContext, roomId, userId, playTime);
                        room.PendingPlayTimeUpdates.Remove(userId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to update play time for {0} in room {1}", userId, room.RoomId);
                    }
                }
            }

            await dbContext.SaveChangesAsync();
        }
        finally
        {
            _userSaveQueue.ReleaseLock();
        }
    }

    public async Task WaitForPlayTimeUpdatesAsync(CancellationToken cancellationToken = default)
    {
        await _playTimeUpdateSemaphoreSlim.WaitAsync(cancellationToken);
        _playTimeUpdateSemaphoreSlim.Release();
    }

    private static async Task IncrementUserPlayTime(BotDbContext dbContext, string roomId, string userId, TimeSpan playTime,
        CancellationToken cancellationToken = default)
    {
        var roomUser = await dbContext.RoomUsers
            .FirstOrDefaultAsync(roomUser => roomUser.Id == userId && roomUser.RoomId == roomId, cancellationToken);

        if (roomUser == null)
        {
            var user = await dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
            if (user == null)
            {
                user = new SavedUser { UserId = userId, UserName = userId };
                await dbContext.Users.AddAsync(user, cancellationToken);
            }

            roomUser = new RoomUser
            {
                Id = userId,
                RoomId = roomId,
                PlayTime = playTime,
                User = user
            };

            await dbContext.RoomUsers.AddAsync(roomUser, cancellationToken);
        }
        else
        {
            roomUser.PlayTime += playTime;
            dbContext.RoomUsers.Update(roomUser);
        }
    }

    public void Dispose()
    {
        _timerRunner?.Dispose();
        _playTimeUpdateSemaphoreSlim.Dispose();
    }
}