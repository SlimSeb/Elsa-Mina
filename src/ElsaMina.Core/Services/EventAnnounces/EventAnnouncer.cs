using System.Globalization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Core.Services.EventAnnounces;

public class EventAnnouncer : IEventAnnouncer
{
    private static readonly TimeSpan COOLDOWN_DURATION = TimeSpan.FromMinutes(30);

    private readonly IConfiguration _configuration;
    private readonly IBot _bot;
    private readonly IResourcesService _resourcesService;
    private readonly IRoomsManager _roomsManager;
    private readonly IClockService _clockService;

    private readonly Dictionary<string, DateTime> _lastAnnouncementTimes = new();
    private readonly Lock _cooldownLock = new();

    public EventAnnouncer(IConfiguration configuration, IBot bot, IResourcesService resourcesService,
        IRoomsManager roomsManager, IClockService clockService)
    {
        _configuration = configuration;
        _bot = bot;
        _resourcesService = resourcesService;
        _roomsManager = roomsManager;
        _clockService = clockService;
    }

    public async Task AnnounceToLinkedRoomsAsync(string sourceRoomId, EventAnnounceType announceType,
        string resourceKey, object[] formatArguments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourceRoomId)
            || _configuration.EventAnnounces == null
            || !_configuration.EventAnnounces.TryGetValue(sourceRoomId, out var receivingRoomsIds))
        {
            return;
        }

        foreach (var receivingRoomId in receivingRoomsIds)
        {
            var room = _roomsManager.GetRoom(receivingRoomId);
            var announcesFilter = room != null
                ? await room.GetParameterValueAsync(Parameter.EventAnnouncesType, cancellationToken)
                : EventAnnouncesTypeValues.All;
            if (!EventAnnouncesTypeValues.Allows(announcesFilter, announceType))
            {
                continue;
            }

            if (!TryConsumeCooldown(receivingRoomId))
            {
                continue;
            }

            var culture = room?.Culture ?? new CultureInfo(_configuration.DefaultLocaleCode);
            var message = string.Format(_resourcesService.GetString(resourceKey, culture), formatArguments);
            _bot.Say(receivingRoomId, $"/wall {message}");
        }
    }

    /// <summary>
    /// Returns whether the receiving room is allowed to be announced to right now, starting a fresh cooldown when it
    /// is. A room that was announced to less than <see cref="COOLDOWN_DURATION"/> ago is skipped to avoid spamming it.
    /// </summary>
    private bool TryConsumeCooldown(string receivingRoomId)
    {
        var now = _clockService.CurrentUtcDateTime;
        lock (_cooldownLock)
        {
            if (_lastAnnouncementTimes.TryGetValue(receivingRoomId, out var lastAnnouncement)
                && now - lastAnnouncement < COOLDOWN_DURATION)
            {
                return false;
            }

            _lastAnnouncementTimes[receivingRoomId] = now;
            return true;
        }
    }
}
