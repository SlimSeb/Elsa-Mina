using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile;

[NamedCommand("avatar",
    Aliases = ["set-avatar", "setavatar", "removeavatar", "remove-avatar", "deleteavatar", "delete-avatar"])]
public class SetAvatarCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public SetAvatarCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "avatar_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string userId;
        string avatarUrl;
        if (context.Command is "removeavatar" or "remove-avatar" or "deleteavatar" or "delete-avatar")
        {
            userId = context.Target.ToLowerAlphaNum();
            avatarUrl = string.Empty;
        }
        else
        {
            var parts = context.Target.Split(",");
            if (parts.Length != 2)
            {
                context.ReplyLocalizedMessage(HelpMessageKey);
                return;
            }

            userId = parts[0].ToLowerAlphaNum();
            avatarUrl = parts[1].Trim();
        }

        try
        {
            await _roomUserDataService.SetUserAvatarAsync(context.RoomId, userId, avatarUrl, cancellationToken);
            context.ReplyLocalizedMessage("avatar_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating avatar");
            context.ReplyLocalizedMessage("avatar_failure", exception.Message);
        }
    }
}