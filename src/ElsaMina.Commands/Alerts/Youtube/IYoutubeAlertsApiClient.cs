namespace ElsaMina.Commands.Alerts.Youtube;

public interface IYoutubeAlertsApiClient : IAlertChannelResolver
{
    Task<IReadOnlyList<string>> GetRecentVideoIdsAsync(string channelId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YoutubeVideo>> GetVideosAsync(IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken = default);
}
