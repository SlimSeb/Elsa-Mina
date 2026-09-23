using System.Collections.Immutable;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Alerts;

public class AlertsManager : IAlertsManager
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly Lock _lock = new();
    private readonly List<AlertSubscription> _alerts = [];

    public AlertsManager(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storedAlerts = await dbContext.ChannelAlerts.ToListAsync(cancellationToken);

        lock (_lock)
        {
            _alerts.Clear();
            _alerts.AddRange(storedAlerts.Select(alert =>
                new AlertSubscription(alert.RoomId, alert.Platform, alert.ChannelId, alert.ChannelName)));
        }
    }

    public IReadOnlyCollection<AlertSubscription> GetAlerts(params string[] platforms)
    {
        lock (_lock)
        {
            return _alerts.Where(alert => platforms.Contains(alert.Platform)).ToImmutableList();
        }
    }

    public IReadOnlyCollection<AlertSubscription> GetRoomAlerts(string roomId)
    {
        lock (_lock)
        {
            return _alerts
                .Where(alert => alert.RoomId == roomId)
                .OrderBy(alert => alert.Platform)
                .ThenBy(alert => alert.ChannelName)
                .ToImmutableList();
        }
    }

    public async Task<bool> AddAlertAsync(string roomId, string platform, AlertChannel channel,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_alerts.Any(alert => alert.RoomId == roomId && alert.Platform == platform
                                                            && alert.ChannelId == channel.ChannelId))
            {
                return false;
            }

            _alerts.Add(new AlertSubscription(roomId, platform, channel.ChannelId, channel.ChannelName));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ChannelAlerts.AddAsync(new ChannelAlert
        {
            RoomId = roomId,
            Platform = platform,
            ChannelId = channel.ChannelId,
            ChannelName = channel.ChannelName
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<AlertSubscription> RemoveAlertAsync(string roomId, string platform, string channel,
        CancellationToken cancellationToken = default)
    {
        var normalizedChannel = channel.TrimStart('@');
        AlertSubscription removedAlert;
        lock (_lock)
        {
            removedAlert = _alerts.FirstOrDefault(alert => alert.RoomId == roomId && alert.Platform == platform
                && (alert.ChannelId == normalizedChannel
                    || string.Equals(alert.ChannelName, normalizedChannel, StringComparison.OrdinalIgnoreCase)));
            if (removedAlert == null)
            {
                return null;
            }

            _alerts.Remove(removedAlert);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await dbContext.ChannelAlerts.FindAsync([roomId, platform, removedAlert.ChannelId],
            cancellationToken);
        if (entry != null)
        {
            dbContext.ChannelAlerts.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return removedAlert;
    }
}
