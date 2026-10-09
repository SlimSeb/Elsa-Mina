using System.Net;
using ElsaMina.Core;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterAlertsService : PollingAlertsService, ITwitterAlertsService
{
    // L'API X a des limites de requêtes très strictes : on interroge peu souvent
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromMinutes(15);
    private const int MAX_TWEETS_ANNOUNCED_PER_POLL = 3;
    private const string TEMPLATE_KEY = "Alerts/Twitter/TweetAlert";

    private readonly IAlertsManager _alertsManager;
    private readonly ITwitterApiClient _twitterApiClient;

    // Présence de la clé = compte déjà initialisé ; valeur = dernier tweet vu (null si aucun)
    private readonly Dictionary<string, string> _lastTweetIds = new();

    public TwitterAlertsService(IAlertsManager alertsManager,
        ITwitterApiClient twitterApiClient,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot)
        : this(alertsManager, twitterApiClient, roomsManager, templatesManager, bot, POLL_INTERVAL)
    {
    }

    public TwitterAlertsService(IAlertsManager alertsManager,
        ITwitterApiClient twitterApiClient,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IBot bot,
        TimeSpan pollInterval) : base(roomsManager, templatesManager, bot, pollInterval)
    {
        _alertsManager = alertsManager;
        _twitterApiClient = twitterApiClient;
    }

    protected override bool IsConfigured => _twitterApiClient.IsConfigured;

    public override async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var alerts = _alertsManager.GetAlerts(AlertPlatforms.TWITTER);
        foreach (var userAlerts in alerts.GroupBy(alert => alert.ChannelId))
        {
            try
            {
                await PollUserAsync(userAlerts.Key, userAlerts.ToList(), cancellationToken);
            }
            catch (HttpException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Log.Warning("X API rate limit reached : skipping the rest of this poll");
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to poll tweets of user {UserId}", userAlerts.Key);
            }
        }
    }

    private async Task PollUserAsync(string userId, IReadOnlyCollection<AlertSubscription> userAlerts,
        CancellationToken cancellationToken)
    {
        var isPriming = !_lastTweetIds.TryGetValue(userId, out var lastTweetId);
        var response = await _twitterApiClient.GetLatestTweetsAsync(userId, lastTweetId, cancellationToken);
        var tweets = response?.Data ?? [];
        if (tweets.Count > 0)
        {
            _lastTweetIds[userId] = response.Meta?.NewestId ?? tweets[0].Id;
        }
        else if (isPriming)
        {
            _lastTweetIds[userId] = null;
        }

        if (isPriming)
        {
            return;
        }

        var username = userAlerts.First().ChannelName;
        var roomIds = userAlerts.Select(alert => alert.RoomId).ToList();
        // L'API renvoie les tweets du plus récent au plus ancien : on les annonce dans l'ordre chronologique
        foreach (var tweet in tweets.Take(MAX_TWEETS_ANNOUNCED_PER_POLL).Reverse())
        {
            await AnnounceAsync(roomIds, TEMPLATE_KEY, new TweetAlertViewModel
            {
                Username = username,
                TweetId = tweet.Id,
                Text = tweet.Text ?? string.Empty
            });
        }
    }
}
