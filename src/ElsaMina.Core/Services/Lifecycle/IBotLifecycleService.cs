namespace ElsaMina.Core.Services.Lifecycle;

/// <summary>
/// Regroupe le travail à effectuer autour du cycle de vie du bot : ce qu'il faut préparer
/// avant la connexion, et ce qu'il faut vider avant l'arrêt.
/// </summary>
public interface IBotLifecycleService
{
    Task OnStartingAsync(CancellationToken cancellationToken = default);
    Task OnExitingAsync();
}
