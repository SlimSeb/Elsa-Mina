using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile;

[NamedCommand("setprofilecolor",
    Aliases = ["set-profile-color", "removeprofilecolor", "remove-profile-color",
               "clearprofilecolor", "clear-profile-color"])]
public class SetProfileColorCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public SetProfileColorCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "set_profile_color_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isClearCommand = context.Command is "removeprofilecolor" or "remove-profile-color"
                                               or "clearprofilecolor" or "clear-profile-color";
        string roomId;
        string colorKey;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            roomId = parts[0].Trim().ToLowerAlphaNum();
            if (string.IsNullOrEmpty(roomId))
            {
                ReplyLocalizedHelpMessage(context);
                return;
            }

            colorKey = isClearCommand || parts.Length < 2 ? string.Empty : parts[1].Trim().ToLowerInvariant();
        }
        else
        {
            roomId = context.RoomId;
            colorKey = isClearCommand ? string.Empty : context.Target.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrEmpty(colorKey) && !EditProfilePanelCommand.PROFILE_COLORS.ContainsKey(colorKey))
        {
            context.ReplyLocalizedMessage("set_profile_color_invalid",
                string.Join(", ", EditProfilePanelCommand.PROFILE_COLORS.Keys));
            return;
        }

        var colorValue = string.IsNullOrEmpty(colorKey)
            ? string.Empty
            : EditProfilePanelCommand.PROFILE_COLORS[colorKey];

        try
        {
            await _roomUserDataService.SetUserBackgroundColorAsync(
                roomId, context.Sender.UserId, colorValue, cancellationToken);
            context.ReplyLocalizedMessage("set_profile_color_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating profile background color");
            context.ReplyLocalizedMessage("set_profile_color_failure", exception.Message);
        }
    }
}
