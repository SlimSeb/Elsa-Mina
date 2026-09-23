namespace ElsaMina.Commands.Alerts.Twitter;

public interface ITwitterApiClient : IAlertChannelResolver
{
    /// <summary>
    /// Returns the user's latest tweets (newest first), excluding replies and retweets.
    /// When <paramref name="sinceTweetId"/> is set, only tweets posted after it are returned.
    /// </summary>
    Task<TwitterTweetsResponse> GetLatestTweetsAsync(string userId, string sinceTweetId,
        CancellationToken cancellationToken = default);
}
