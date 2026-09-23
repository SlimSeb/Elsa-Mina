namespace ElsaMina.Commands.Alerts.Twitch;

public interface ITwitchApiClient : IAlertChannelResolver
{
    Task<IReadOnlyList<TwitchStream>> GetLiveStreamsAsync(IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}
