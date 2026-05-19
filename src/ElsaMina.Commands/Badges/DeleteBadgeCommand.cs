using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Badges;

[NamedCommand("deletebadge", Aliases = ["deletetrophy", "delete-badge", "delete-trophy"])]
public class DeleteBadgeCommand : Command
{
    private readonly IBotDbContextFactory _factory;

    public DeleteBadgeCommand(IBotDbContextFactory factory)
    {
        _factory = factory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "badge_delete_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var badgeId = context.Target.ToLowerAlphaNum();
            await using var dbContext = await _factory.CreateDbContextAsync(cancellationToken);
            var badge = await dbContext.Badges.FindAsync([badgeId, context.RoomId], cancellationToken);
            if (badge == null)
            {
                context.ReplyLocalizedMessage("badge_delete_doesnt_exist", badgeId);
                return;
            }
            
            dbContext.Badges.Remove(badge);
            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("badge_delete_success", badgeId);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while deleting badge");
            context.ReplyLocalizedMessage("badge_delete_failure", exception.Message);
        }
    }
}