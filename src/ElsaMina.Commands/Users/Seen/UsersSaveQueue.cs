using System.Collections.Concurrent;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Users.Seen;

/// <summary>
/// Buffers users' "last seen" updates, then saves them in batches.
/// Several updates for the same user are merged: only the most recent one is saved
/// </summary>
public sealed class UserSaveQueue : IUserSaveQueue, IBotLifecycleParticipant
{
    private sealed record PendingUserSave(
        string RawUserName,
        string RoomId,
        UserAction Action,
        DateTimeOffset LastSeenTime);

    // userId -> latest pending update for that user
    private readonly ConcurrentDictionary<string, PendingUserSave> _pendingUsers = new();
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IClockService _clockService;
    private readonly PeriodicTimer _timer;
    private readonly int _batchSize;

    private readonly SemaphoreSlim _flushLock = new(1, 1);
    // Guards _currentFlushTask so a flush already running is shared
    private readonly Lock _flushTaskLock = new();

    private Task _currentFlushTask;
    private readonly CancellationTokenSource _timerBasedBackgroundSaveCts = new();

    public UserSaveQueue(
        IBotDbContextFactory dbContextFactory,
        ICommandsConfiguration configuration, IClockService clockService)
    {
        _dbContextFactory = dbContextFactory;
        _clockService = clockService;
        _batchSize = configuration.UserUpdateBatchSize;
        _timer = new PeriodicTimer(configuration.UserUpdateFlushInterval);

        _ = Task.Run(BackgroundFlushAsync, _timerBasedBackgroundSaveCts.Token);
    }

    public void Enqueue(string userName, string roomId, UserAction action)
    {
        var userId = userName.ToLowerAlphaNum();
        _pendingUsers[userId] = new PendingUserSave(userName, roomId, action, _clockService.CurrentUtcDateTimeOffset);

        if (_pendingUsers.Count >= _batchSize)
        {
            _ = FlushAsync(CancellationToken.None);
        }
    }

    private async Task BackgroundFlushAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_timerBasedBackgroundSaveCts.Token))
            {
                await FlushAsync(_timerBasedBackgroundSaveCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while flushing user updates (timer based)");
        }
    }

    public Task OnExitingAsync(CancellationToken cancellationToken) => FlushAsync(cancellationToken);

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        lock (_flushTaskLock)
        {
            // Reuse the running flush instead of starting several in parallel
            if (_currentFlushTask?.IsCompleted == false)
            {
                return _currentFlushTask;
            }

            _currentFlushTask = FlushInternalAsync(cancellationToken).ContinueWith(task =>
                {
                    lock (_flushTaskLock)
                    {
                        _currentFlushTask = null;
                    }

                    return task;
                }, TaskScheduler.Default)
                .Unwrap();

            return _currentFlushTask;
        }
    }

    private async Task FlushInternalAsync(CancellationToken cancellationToken)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            // Loop to pick up updates that arrived while the current batch was being saved.
            while (!_pendingUsers.IsEmpty)
            {
                var batch = _pendingUsers.ToArray();
                _pendingUsers.Clear();

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var userIds = batch.Select(kvp => kvp.Key).ToList();

                var existingUsers = await dbContext.Users
                    .Where(user => userIds.Contains(user.UserId))
                    .ToDictionaryAsync(u => u.UserId, cancellationToken);

                Log.Debug("Saving user ids : {0}", string.Join(", ", userIds));
                foreach (var (userId, pendingUser) in batch)
                {
                    ApplyPendingUser(dbContext, existingUsers, userId, pendingUser);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private static void ApplyPendingUser(BotDbContext dbContext, Dictionary<string, SavedUser> existingUsers,
        string userId, PendingUserSave pendingUser)
    {
        var userName = ExtractUserName(pendingUser.RawUserName);

        if (!existingUsers.TryGetValue(userId, out var user))
        {
            dbContext.Users.Add(new SavedUser
            {
                UserId = userId,
                UserName = userName,
                LastOnline = pendingUser.LastSeenTime,
                LastSeenRoomId = pendingUser.RoomId,
                LastSeenAction = pendingUser.Action
            });
            return;
        }

        if (user.UserName != userName)
        {
            user.UserName = userName;
        }

        if (user.LastOnline != pendingUser.LastSeenTime)
        {
            user.LastOnline = pendingUser.LastSeenTime;
        }

        if (user.LastSeenRoomId != pendingUser.RoomId)
        {
            user.LastSeenRoomId = pendingUser.RoomId;
        }

        if (user.LastSeenAction != pendingUser.Action)
        {
            user.LastSeenAction = pendingUser.Action;
        }
    }

    public async Task WaitForFlushAsync(CancellationToken cancellationToken = default)
    {
        Task flushTask;

        lock (_flushTaskLock)
        {
            flushTask = _currentFlushTask;
        }

        if (flushTask != null)
        {
            await flushTask.WaitAsync(cancellationToken);
        }
    }

    public async Task AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        // Lets other services suspend flushes during their own related writes
        await _flushLock.WaitAsync(cancellationToken);
    }

    public void ReleaseLock()
    {
        _flushLock.Release();
    }

    public async ValueTask DisposeAsync()
    {
        await _timerBasedBackgroundSaveCts.CancelAsync();
        _timerBasedBackgroundSaveCts.Dispose();
        _timer.Dispose();
        await FlushAsync(CancellationToken.None);
        await WaitForFlushAsync(CancellationToken.None);
    }

    private static string ExtractUserName(string rawUserName)
    {
        if (string.IsNullOrEmpty(rawUserName))
        {
            return string.Empty;
        }

        return char.IsLetterOrDigit(rawUserName[0])
            ? rawUserName
            : rawUserName[1..];
    }
}
