namespace ElsaMina.Commands.Alerts;

public interface IAlertChannelResolver
{
    bool IsConfigured { get; }
    bool CanResolve(string platform);

    /// <summary>
    /// Finds the channel matching the user input on the platform, or returns null when it does not exist.
    /// </summary>
    Task<AlertChannel> ResolveChannelAsync(string input, CancellationToken cancellationToken = default);
}
