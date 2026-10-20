namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Persiste les droits donnés aux users. Core dépend juste de ce port, l'implémentation bdd est dans
/// ElsaMina.DataAccess
/// </summary>
public interface IPermissionGrantRepository
{
    Task<IReadOnlyList<PermissionGrant>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute le droit, ou met à jour celui qui a la même room, le même user, le même type et la même cible
    /// </summary>
    Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default);

    Task DeleteAsync(PermissionGrant grant, CancellationToken cancellationToken = default);

    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
