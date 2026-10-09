using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchLiveAlertsService : PollingAlertsService, ITwitchLiveAlertsService
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromMinutes(2);
    private const string TEMPLATE_KEY = "Alerts/Twitch/TwitchLiveAlert";

    private readonly IAlertsManager _alertsManager;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly IClockService _clockService;

    // Dernier live annoncé par chaîne : évite de ré-annoncer un live déjà signalé,
    // même si l'API l'omet ponctuellement entre deux polls
    private readonly Dictionary<string, string> _lastAnnouncedStreamIds = new();
    private bool _isFirstPoll = true;

    public TwitchLiveAlertsService(IAlertsManager alertsManager,
        ITwitchApiClient twitchApiClient,
        IClockService clockService,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot)
        : this(alertsManager, twitchApiClient, clockService, roomsManager, templatesManager, bot, POLL_INTERVAL)
    {
    }

    public TwitchLiveAlertsService(IAlertsManager alertsManager,
        ITwitchApiClient twitchApiClient,
        IClockService clockService,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot,
        TimeSpan pollInterval) : base(roomsManager, templatesManager, bot, pollInterval)
    {
        _alertsManager = alertsManager;
        _twitchApiClient = twitchApiClient;
        _clockService = clockService;
    }

    protected override bool IsConfigured => _twitchApiClient.IsConfigured;

    public override async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var alerts = _alertsManager.GetAlerts(AlertPlatforms.TWITCH);
        if (alerts.Count == 0)
        {
            _isFirstPoll = false;
            return;
        }

        var userIds = alerts.Select(alert => alert.ChannelId).Distinct().ToList();
        var liveStreams = await _twitchApiClient.GetLiveStreamsAsync(userIds, cancellationToken);
        var now = _clockService.CurrentUtcDateTimeOffset;

        foreach (var stream in liveStreams)
        {
            if (stream.UserId == null
                || (_lastAnnouncedStreamIds.TryGetValue(stream.UserId, out var lastStreamId)
                    && lastStreamId == stream.Id))
            {
                continue;
            }

            _lastAnnouncedStreamIds[stream.UserId] = stream.Id;

            // Au démarrage du bot, on ne ré-annonce pas les lives commencés bien avant
            if (_isFirstPoll && now - stream.StartedAt > PollInterval * 2)
            {
                continue;
            }

            var roomIds = alerts
                .Where(alert => alert.ChannelId == stream.UserId)
                .Select(alert => alert.RoomId);
            await AnnounceAsync(roomIds, TEMPLATE_KEY, new TwitchLiveAlertViewModel
            {
                ChannelLogin = stream.UserLogin,
                ChannelDisplayName = string.IsNullOrWhiteSpace(stream.UserName) ? stream.UserLogin : stream.UserName,
                Title = stream.Title ?? string.Empty,
                GameName = stream.GameName ?? string.Empty,
                ThumbnailUrl = (stream.ThumbnailUrl ?? string.Empty)
                    .Replace("{width}", "320")
                    .Replace("{height}", "180")
            });
        }

        _isFirstPoll = false;
    }
}
