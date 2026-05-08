using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile;

[NamedCommand("setemoji", Aliases = ["set-emoji", "removeemoji", "remove-emoji", "clearemoji", "clear-emoji"])]
public class SetEmojiCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public SetEmojiCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "set_emoji_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isClearCommand = context.Command is "removeemoji" or "remove-emoji" or "clearemoji" or "clear-emoji";
        string roomId;
        string emoji;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            roomId = parts[0].Trim().ToLowerAlphaNum();
            if (string.IsNullOrEmpty(roomId))
            {
                ReplyLocalizedHelpMessage(context);
                return;
            }

            emoji = isClearCommand || parts.Length < 2 ? string.Empty : parts[1].Trim();
        }
        else
        {
            roomId = context.RoomId;
            emoji = isClearCommand ? string.Empty : context.Target.Trim();
        }

        if (!string.IsNullOrEmpty(emoji) && !emoji.IsSingleEmoji())
        {
            context.ReplyLocalizedMessage("set_emoji_invalid");
            return;
        }

        try
        {
            await _roomUserDataService.SetUserEmojiAsync(roomId, context.Sender.UserId, emoji, cancellationToken);
            context.ReplyLocalizedMessage("set_emoji_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating profile emoji");
            context.ReplyLocalizedMessage("set_emoji_failure", exception.Message);
        }
    }
}
