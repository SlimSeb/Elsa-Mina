namespace ElsaMina.Core.Services.Lifecycle;

/// <summary>
/// A service that has work to do when the bot starts (load data before connecting) or stops
/// (flush pending writes before the process exits). Register it with
/// <c>.As&lt;IBotLifecycleParticipant&gt;()</c> in its module; Core runs every registered participant.
/// </summary>
public interface IBotLifecycleParticipant
{
    Task OnStartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnExitingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
