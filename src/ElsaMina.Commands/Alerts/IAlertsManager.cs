namespace ElsaMina.Commands.Alerts;

public interface IAlertsManager
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    IReadOnlyCollection<AlertSubscription> GetAlerts(params string[] platforms);
    IReadOnlyCollection<AlertSubscription> GetRoomAlerts(string roomId);

    Task<bool> AddAlertAsync(string roomId, string platform, AlertChannel channel,
        CancellationToken cancellationToken = default);

    Task<AlertSubscription> RemoveAlertAsync(string roomId, string platform, string channel,
        CancellationToken cancellationToken = default);
}
