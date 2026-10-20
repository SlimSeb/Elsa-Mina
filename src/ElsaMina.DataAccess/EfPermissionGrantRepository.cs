using ElsaMina.Core.Services.Authorization;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.DataAccess;

public class EfPermissionGrantRepository : IPermissionGrantRepository
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public EfPermissionGrantRepository(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<PermissionGrant>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var savedGrants = await dbContext.PermissionGrants.AsNoTracking().ToListAsync(cancellationToken);

        var grants = new List<PermissionGrant>();
        foreach (var savedGrant in savedGrants)
        {
            if (!Enum.TryParse<PermissionGrantKind>(savedGrant.Kind, out var kind))
            {
                Log.Warning("Ignoring permission grant of unknown kind {0} for {1} in {2}", savedGrant.Kind,
                    savedGrant.UserId, savedGrant.RoomId);
                continue;
            }

            grants.Add(new PermissionGrant(savedGrant.RoomId, savedGrant.UserId, kind, savedGrant.Target,
                savedGrant.GrantedBy, savedGrant.GrantedAt, savedGrant.ExpiresAt));
        }

        return grants;
    }

    public async Task SaveAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.PermissionGrants.FindAsync(GetKey(grant), cancellationToken);
        if (existing == null)
        {
            existing = new SavedPermissionGrant
            {
                RoomId = grant.RoomId,
                UserId = grant.UserId,
                Kind = grant.Kind.ToString(),
                Target = grant.Target
            };
            await dbContext.PermissionGrants.AddAsync(existing, cancellationToken);
        }

        existing.GrantedBy = grant.GrantedBy;
        existing.GrantedAt = grant.GrantedAt;
        existing.ExpiresAt = grant.ExpiresAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PermissionGrant grant, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.PermissionGrants.FindAsync(GetKey(grant), cancellationToken);
        if (existing == null)
        {
            return;
        }

        dbContext.PermissionGrants.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expiredGrants = await dbContext.PermissionGrants
            .Where(grant => grant.ExpiresAt != null && grant.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (expiredGrants.Count == 0)
        {
            return;
        }

        dbContext.PermissionGrants.RemoveRange(expiredGrants);
        await dbContext.SaveChangesAsync(cancellationToken);
        Log.Information("Deleted {0} expired permission grants", expiredGrants.Count);
    }

    private static object[] GetKey(PermissionGrant grant) =>
        [grant.RoomId, grant.UserId, grant.Kind.ToString(), grant.Target];
}
