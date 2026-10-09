namespace ElsaMina.Core.Services.Lifecycle;

/// <summary>
/// Un service qui a du taf au démarrage du bot (charger des données avant de se connecter) ou à l'arrêt
/// (flush les écritures en attente avant que le process se termine). L'enregistrer avec
/// <c>.As&lt;IBotLifecycleParticipant&gt;()</c> dans son module, Core lance tous les participants enregistrés
/// </summary>
public interface IBotLifecycleParticipant
{
    Task OnStartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnExitingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
