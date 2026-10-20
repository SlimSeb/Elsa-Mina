using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Authorization;

public class PermissionGrantStore : IPermissionGrantStore, IBotLifecycleParticipant
{
    private readonly IPermissionGrantRepository _repository;
    private readonly IClockService _clockService;
    private readonly Lock _grantsLock = new();

    // Les écritures passent une par une, comme ça la bdd et la mémoire finissent toujours dans le même état
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private Dictionary<(string RoomId, string UserId), IReadOnlyList<PermissionGrant>> _grantsByUser = new();

    public PermissionGrantStore(IPermissionGrantRepository repository, IClockService clockService)
    {
        _repository = repository;
        _clockService = clockService;
    }

    public async Task OnStartingAsync(CancellationToken cancellationToken)
    {
        var now = _clockService.CurrentUtcDateTimeOffset;
        await _repository.DeleteExpiredAsync(now, cancellationToken);
        var grants = await _repository.LoadAllAsync(cancellationToken);
        var grantsByUser = grants
            .Where(grant => grant.IsActiveAt(now))
            .GroupBy(grant => (grant.RoomId, grant.UserId))
            .ToDictionary(group => group.Key, IReadOnlyList<PermissionGrant> (group) => group.ToList());

        lock (_grantsLock)
        {
            _grantsByUser = grantsByUser;
        }

        Log.Information("Loaded {0} permission grants", grantsByUser.Values.Sum(userGrants => userGrants.Count));
    }

    public IReadOnlyList<PermissionGrant> GetActiveGrants(string roomId, string userId)
    {
        IReadOnlyList<PermissionGrant> userGrants;
        lock (_grantsLock)
        {
            if (!_grantsByUser.TryGetValue((roomId, userId), out userGrants))
            {
                return [];
            }
        }

        var now = _clockService.CurrentUtcDateTimeOffset;
        return userGrants.Where(grant => grant.IsActiveAt(now)).ToList();
    }

    public IReadOnlyList<PermissionGrant> GetActiveGrants(string roomId)
    {
        List<PermissionGrant> roomGrants;
        lock (_grantsLock)
        {
            roomGrants = _grantsByUser
                .Where(entry => entry.Key.RoomId == roomId)
                .SelectMany(entry => entry.Value)
                .ToList();
        }

        var now = _clockService.CurrentUtcDateTimeOffset;
        return roomGrants.Where(grant => grant.IsActiveAt(now)).ToList();
    }

    public async Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var previous = Find(grant.RoomId, grant.UserId, grant.Kind, grant.Target);

            // Pour un rang la cible peut changer (+ => %), et la ligne d'avant a alors une autre clé en bdd
            if (previous != null && previous.Target != grant.Target)
            {
                await _repository.DeleteAsync(previous, cancellationToken);
            }

            await _repository.SaveAsync(grant, cancellationToken);
            UpdateUserGrants(grant.RoomId, grant.UserId,
                userGrants => [..userGrants.Where(existing => !existing.IsFor(grant.Kind, grant.Target)), grant]);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task<bool> RemoveAsync(string roomId, string userId, PermissionGrantKind kind, string target,
        CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var existing = Find(roomId, userId, kind, target);
            if (existing == null)
            {
                return false;
            }

            await _repository.DeleteAsync(existing, cancellationToken);
            UpdateUserGrants(roomId, userId,
                userGrants => userGrants.Where(grant => !grant.IsFor(kind, target)).ToList());

            // Un droit déjà expiré comptait plus : on nettoie quand même, mais pour le user y avait rien à retirer
            return existing.IsActiveAt(_clockService.CurrentUtcDateTimeOffset);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private PermissionGrant Find(string roomId, string userId, PermissionGrantKind kind, string target)
    {
        lock (_grantsLock)
        {
            return _grantsByUser.GetValueOrDefault((roomId, userId))?.FirstOrDefault(grant => grant.IsFor(kind, target));
        }
    }

    private void UpdateUserGrants(string roomId, string userId,
        Func<IReadOnlyList<PermissionGrant>, IReadOnlyList<PermissionGrant>> update)
    {
        var now = _clockService.CurrentUtcDateTimeOffset;
        lock (_grantsLock)
        {
            // On en profite pour oublier les droits expirés de ce user
            var key = (roomId, userId);
            var userGrants = update(_grantsByUser.GetValueOrDefault(key) ?? [])
                .Where(grant => grant.IsActiveAt(now))
                .ToList();

            if (userGrants.Count == 0)
            {
                _grantsByUser.Remove(key);
            }
            else
            {
                _grantsByUser[key] = userGrants;
            }
        }
    }
}
