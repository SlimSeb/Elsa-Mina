namespace ElsaMina.Core.Services.Lifecycle;

/// <summary>
/// Lance les <see cref="IBotLifecycleParticipant"/> enregistrés : ce qu'il faut préparer avant de se connecter,
/// et ce qu'il faut flush avant de s'arrêter
/// </summary>
public interface IBotLifecycleService
{
    Task OnStartingAsync(CancellationToken cancellationToken = default);
    Task OnExitingAsync(CancellationToken cancellationToken = default);
}
