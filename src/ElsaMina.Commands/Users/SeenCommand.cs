using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Users;

[NamedCommand("seen")]
public class SeenCommand : Command
{
    private readonly IBotDbContextFactory _botDbContextFactory;

    public SeenCommand(IBotDbContextFactory botDbContextFactory)
    {
        _botDbContextFactory = botDbContextFactory;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsWhitelistOnly => true;

    public override string HelpMessageKey => "seen_command_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var targetUserId = context.Target?.ToLowerAlphaNum();
        if (string.IsNullOrEmpty(targetUserId))
        {
            ReplyLocalizedHelpMessage(context, rankAware: true);
            return;
        }

        SavedUser savedUser;
        try
        {
            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
            savedUser = await dbContext.Users.FindAsync([targetUserId], cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error("Error while fetching user", exception);
            context.ReplyRankAwareLocalizedMessage("seen_command_error", targetUserId, exception.Message);
            return;
        }

        if (savedUser == null)
        {
            context.ReplyRankAwareLocalizedMessage("seen_command_not_found", targetUserId);
            return;
        }

        var actionStringId = savedUser.LastSeenAction switch
        {
            UserAction.Leaving => "seen_command_action_leaving",
            UserAction.Joining => "seen_command_action_joining",
            UserAction.Chatting => "seen_command_action_chatting",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(actionStringId) ||
            savedUser.LastOnline == null ||
            string.IsNullOrWhiteSpace(savedUser.LastSeenRoomId))
        {
            context.ReplyRankAwareLocalizedMessage("seen_command_not_found", targetUserId);
            return;
        }

        var actionLabel = context.GetString(actionStringId);
        var actionDate = TimeZoneInfo.ConvertTime(savedUser.LastOnline.Value, context.Room.TimeZone)
            .ToString("g", context.Culture);
        var lastSeenInRoom = savedUser.LastSeenRoomId;
        var userName = string.IsNullOrWhiteSpace(savedUser.UserName) ? targetUserId : savedUser.UserName;

        context.ReplyRankAwareLocalizedMessage("seen_command_last_seen", userName, actionDate, actionLabel,
            lastSeenInRoom);
    }
}