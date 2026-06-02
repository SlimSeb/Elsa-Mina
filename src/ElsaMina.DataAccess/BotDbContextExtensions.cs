using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.DataAccess;

public static class BotDbContextExtensions
{
    /// <summary>
    /// Ensures a <see cref="SavedUser"/> row exists for the given user id, creating a stub one when it does not.
    /// Call this before inserting a user-owned row (game scores, points, ...) so that the foreign key to the user
    /// is always satisfied even if the user has never been saved before. The stub is added to the change tracker and
    /// persisted by the next <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>.
    /// </summary>
    public static async Task EnsureUserExistsAsync(this BotDbContext dbContext, string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user != null)
        {
            return;
        }

        await dbContext.Users.AddAsync(new SavedUser
        {
            UserId = userId,
            UserName = userId
        }, cancellationToken);
    }
}
