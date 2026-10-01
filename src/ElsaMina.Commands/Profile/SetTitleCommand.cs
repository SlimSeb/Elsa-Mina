using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile;

[NamedCommand("title", "settitle", "set-title", "set-bio", "setbio", "removetitle", "deletetitle",
    "delete-title", "remove-title", "addtitle", "addbio", "deletebio", "removebio")]
public class SetTitleCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public SetTitleCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "title_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string userId;
        string title;

        if (context.Command is "removetitle" or "deletetitle" or "delete-title" or "remove-title" or "deletebio"
            or "removebio")
        {
            userId = context.Target.ToLowerAlphaNum();
            title = string.Empty;
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
            title = parts[1].Trim();
        }

        try
        {
            await _roomUserDataService.SetUserTitleAsync(context.RoomId, userId, title, cancellationToken);
            context.ReplyLocalizedMessage("title_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating avatar");
            context.ReplyLocalizedMessage("title_failure", exception.Message);
        }
    }
}