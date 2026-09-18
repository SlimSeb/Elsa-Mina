using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Badges;

[NamedCommand("editbadge", Aliases = ["edit-badge", "updatebadge", "update-badge"])]
public class EditBadgeCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public EditBadgeCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "badge_edit_help_message";
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length < 3)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var badgeId = parts[0].Trim().ToLowerAlphaNum();
        var newName = parts[1].Trim();
        var roomId = context.RoomId;
        string newImage;
        bool? newIsTrophy = null;
        bool? newIsTeamTournament = null;
        if (parts.Length >= 6)
        {
            roomId = parts[^1].Trim().ToLowerAlphaNum();
            newIsTeamTournament = parts[^2].Trim().ToBoolean();
            newIsTrophy = parts[^3].Trim().ToBoolean();
            newImage = string.Join(",", parts[2..^3]).Trim();
        }
        else if (parts.Length == 5)
        {
            roomId = parts[^1].Trim().ToLowerAlphaNum();
            newIsTrophy = parts[^2].Trim().ToBoolean();
            newImage = string.Join(",", parts[2..^2]).Trim();
        }
        else if (parts.Length > 3)
        {
            roomId = parts[^1].Trim().ToLowerAlphaNum();
            newImage = string.Join(",", parts[2..^1]).Trim();
        }
        else
        {
            newImage = string.Join(",", parts[2..]).Trim();
        }

        if (context.IsPrivateMessage && !await context.HasSufficientRankInRoom(roomId, RequiredRank, cancellationToken))
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var badge = await dbContext.Badges.FindAsync([badgeId, roomId], cancellationToken);
        if (badge == null)
        {
            context.ReplyLocalizedMessage("badge_edit_not_found", badgeId);
            return;
        }

        badge.Name = newName;
        badge.Image = newImage;
        if (newIsTrophy.HasValue)
        {
            badge.IsTrophy = newIsTrophy.Value;
        }

        if (newIsTeamTournament.HasValue)
        {
            badge.IsTeamTournament = newIsTeamTournament.Value;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            context.ReplyLocalizedMessage("badge_edit_success", badgeId);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not edit badge");
            context.ReplyLocalizedMessage("badge_edit_failure", exception.Message);
        }
    }
}