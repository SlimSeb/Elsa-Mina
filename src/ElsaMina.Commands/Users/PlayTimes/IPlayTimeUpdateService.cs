namespace ElsaMina.Commands.Users.PlayTimes;

public interface IPlayTimeUpdateService : IDisposable
{
    void Initialize();
    Task ProcessPendingPlayTimeUpdatesAsync();
    Task WaitForPlayTimeUpdatesAsync(CancellationToken cancellationToken = default);
}
