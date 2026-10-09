using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeAlertsService : PollingAlertsService, IYoutubeAlertsService
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MAX_VIDEO_AGE = TimeSpan.FromDays(1);
    private const string TEMPLATE_KEY = "Alerts/Youtube/YoutubeAlert";
    private const string LIVE_CONTENT = "live";
    private const string UPCOMING_CONTENT = "upcoming";

    private readonly IAlertsManager _alertsManager;
    private readonly IYoutubeAlertsApiClient _youtubeApiClient;
    private readonly IClockService _clockService;

    private readonly Dictionary<string, YoutubeVideoState> _videoStates = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _recentVideoIdsByChannel = new();

    public YoutubeAlertsService(IAlertsManager alertsManager,
        IYoutubeAlertsApiClient youtubeApiClient,
        IClockService clockService,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot)
        : this(alertsManager, youtubeApiClient, clockService, roomsManager, templatesManager, bot, POLL_INTERVAL)
    {
    }

    public YoutubeAlertsService(IAlertsManager alertsManager,
        IYoutubeAlertsApiClient youtubeApiClient,
        IClockService clockService,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot,
        TimeSpan pollInterval) : base(roomsManager, templatesManager, bot, pollInterval)
    {
        _alertsManager = alertsManager;
        _youtubeApiClient = youtubeApiClient;
        _clockService = clockService;
    }

    protected override bool IsConfigured => _youtubeApiClient.IsConfigured;

    public override async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var alerts = _alertsManager.GetAlerts(AlertPlatforms.YOUTUBE, AlertPlatforms.YOUTUBE_LIVE);
        foreach (var channelId in alerts.Select(alert => alert.ChannelId).Distinct())
        {
            try
            {
                await PollChannelAsync(channelId, alerts, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to poll YouTube channel {ChannelId}", channelId);
            }
        }
    }

    private async Task PollChannelAsync(string channelId, IReadOnlyCollection<AlertSubscription> alerts,
        CancellationToken cancellationToken)
    {
        var recentVideoIds = await _youtubeApiClient.GetRecentVideoIdsAsync(channelId, cancellationToken);

        // Première fois qu'on voit la chaîne : on mémorise les vidéos existantes sans les annoncer
        var isPriming = !_recentVideoIdsByChannel.TryGetValue(channelId, out var previousVideoIds);
        _recentVideoIdsByChannel[channelId] = recentVideoIds;
        if (previousVideoIds != null)
        {
            foreach (var droppedVideoId in previousVideoIds.Except(recentVideoIds))
            {
                _videoStates.Remove(droppedVideoId);
            }
        }

        var videoIdsToCheck = recentVideoIds
            .Where(videoId => !_videoStates.TryGetValue(videoId, out var state) || state == YoutubeVideoState.Upcoming)
            .ToList();
        if (videoIdsToCheck.Count == 0)
        {
            return;
        }

        var videos = await _youtubeApiClient.GetVideosAsync(videoIdsToCheck, cancellationToken);
        foreach (var videoId in videoIdsToCheck.Except(videos.Select(video => video.Id)))
        {
            // Vidéo privée ou supprimée
            _videoStates[videoId] = YoutubeVideoState.Handled;
        }

        foreach (var video in videos)
        {
            await HandleVideoAsync(video, channelId, alerts, isPriming);
        }
    }

    private async Task HandleVideoAsync(YoutubeVideo video, string channelId,
        IReadOnlyCollection<AlertSubscription> alerts, bool isPriming)
    {
        var now = _clockService.CurrentUtcDateTimeOffset;
        var snippet = video.Snippet;
        switch (snippet?.LiveBroadcastContent)
        {
            case UPCOMING_CONTENT:
                _videoStates[video.Id] = YoutubeVideoState.Upcoming;
                return;
            case LIVE_CONTENT:
                _videoStates[video.Id] = YoutubeVideoState.Handled;
                var startTime = video.LiveStreamingDetails?.ActualStartTime ?? snippet.PublishedAt;
                if (isPriming && now - startTime > PollInterval * 2)
                {
                    return;
                }

                await AnnounceVideoAsync(video, channelId, alerts, AlertPlatforms.YOUTUBE_LIVE, isLive: true);
                return;
            default:
                _videoStates[video.Id] = YoutubeVideoState.Handled;
                var isFinishedLiveStream = video.LiveStreamingDetails != null;
                if (isPriming || isFinishedLiveStream || snippet == null || now - snippet.PublishedAt > MAX_VIDEO_AGE)
                {
                    return;
                }

                await AnnounceVideoAsync(video, channelId, alerts, AlertPlatforms.YOUTUBE, isLive: false);
                return;
        }
    }

    private Task AnnounceVideoAsync(YoutubeVideo video, string channelId,
        IReadOnlyCollection<AlertSubscription> alerts, string platform, bool isLive)
    {
        var roomIds = alerts
            .Where(alert => alert.ChannelId == channelId && alert.Platform == platform)
            .Select(alert => alert.RoomId)
            .ToList();
        if (roomIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var thumbnail = video.Snippet.Thumbnails?.Medium ?? video.Snippet.Thumbnails?.Default;
        return AnnounceAsync(roomIds, TEMPLATE_KEY, new YoutubeAlertViewModel
        {
            IsLive = isLive,
            VideoId = video.Id,
            Title = video.Snippet.Title ?? string.Empty,
            ChannelTitle = video.Snippet.ChannelTitle ?? string.Empty,
            ThumbnailUrl = thumbnail?.Url ?? string.Empty
        });
    }
}
