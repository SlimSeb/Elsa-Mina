namespace ElsaMina.Core.Services.Dispatch;

public interface IIncomingMessageDispatcher
{
    /// <summary>
    /// Planifie une frame reçue du serveur. Les frames d'une même room sont traitées l'une après l'autre, dans
    /// l'ordre d'arrivée ; celles de rooms différentes en parallèle.
    /// Rend la main dès que la frame est planifiée, pas quand elle est traitée.
    /// </summary>
    Task DispatchAsync(string frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se termine quand toutes les frames planifiées jusqu'ici ont été traitées
    /// </summary>
    Task WhenIdleAsync(CancellationToken cancellationToken = default);
}
