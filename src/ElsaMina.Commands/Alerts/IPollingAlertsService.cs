namespace ElsaMina.Commands.Alerts;

public interface IPollingAlertsService : IDisposable
{
    void Start();
    Task PollOnceAsync(CancellationToken cancellationToken = default);
}
