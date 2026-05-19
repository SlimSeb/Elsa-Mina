using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Badges;

[NamedCommand("add-badge", Aliases = ["addbadge", "new-badge", "newbadge", "add-trophy", "newtrophy", "new-trophy"])]
public class AddBadgeCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public AddBadgeCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "badge_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var arguments = context.Target.Split(",");
        if (arguments.Length < 2)
        {
            context.ReplyLocalizedMessage("badge_help_message");
            return;
        }

        var name = arguments[0].Trim();
        var image = arguments[1].Trim();
        var isTrophy = context.Command is "add-trophy" or "newtrophy" or "new-trophy";
        var badgeId = name.ToLowerAlphaNum();

        string roomId;
        if (context.IsPrivateMessage)
        {
            if (arguments.Length < 3 || string.IsNullOrWhiteSpace(arguments[2]))
            {
                context.ReplyLocalizedMessage("badge_pm_missing_room");
                return;
            }
            roomId = arguments[2].Trim().ToLowerAlphaNum();

            if (!await context.HasSufficientRankInRoom(roomId, Rank.Driver, cancellationToken))
            {
                context.ReplyLocalizedMessage("badge_pm_insufficient_rank");
                return;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingBadge = await dbContext.Badges.FindAsync([badgeId, roomId], cancellationToken: cancellationToken);
        if (existingBadge != null)
        {
            context.ReplyLocalizedMessage("badge_add_already_exist", name);
            return;
        }

        try
        {
            await dbContext.Badges.AddAsync(new Badge
            {
                Name = name,
                Image = image,
                Id = badgeId,
                IsTrophy = isTrophy,
                RoomId = roomId
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("badge_add_success_message");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not add badge");
            context.ReplyLocalizedMessage("badge_add_failure_message", exception.Message);
        }
    }
}
