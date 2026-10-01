namespace ElsaMina.Core.Services.Lifecycle;

/// <summary>
/// Runs the registered <see cref="IBotLifecycleParticipant"/>s: what to prepare before connecting,
/// and what to flush before stopping.
/// </summary>
public interface IBotLifecycleService
{
    Task OnStartingAsync(CancellationToken cancellationToken = default);
    Task OnExitingAsync(CancellationToken cancellationToken = default);
}
