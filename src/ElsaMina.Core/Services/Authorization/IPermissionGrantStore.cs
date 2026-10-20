namespace ElsaMina.Core.Services.Authorization;

/// <summary>
/// Les droits donnés aux users, gardés en mémoire pour que les vérifs de rang restent synchrones. Un droit expiré
/// est jamais renvoyé, même s'il est encore en bdd
/// </summary>
public interface IPermissionGrantStore
{
    IReadOnlyList<PermissionGrant> GetActiveGrants(string roomId, string userId);

    IReadOnlyList<PermissionGrant> GetActiveGrants(string roomId);

    /// <summary>
    /// Enregistre le droit. Il remplace celui que le user avait déjà sur la même cible (cf
    /// <see cref="PermissionGrant.IsFor"/>), comme ça redonner un droit change juste sa durée
    /// </summary>
    Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retire le droit du user sur cette cible (pour un rang, son droit de rang quel qu'il soit). Renvoie false s'il
    /// en avait pas
    /// </summary>
    Task<bool> RemoveAsync(string roomId, string userId, PermissionGrantKind kind, string target,
        CancellationToken cancellationToken = default);
}
