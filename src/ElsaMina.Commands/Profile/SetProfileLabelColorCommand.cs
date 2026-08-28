using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile;

[NamedCommand("setprofilelabelcolor",
    Aliases = ["set-profile-label-color", "removeprofilelabelcolor", "remove-profile-label-color",
               "clearprofilelabelcolor", "clear-profile-label-color"])]
public class SetProfileLabelColorCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;
    private readonly IEditProfilePanelService _editProfilePanelService;

    public SetProfileLabelColorCommand(IRoomUserDataService roomUserDataService,
        IEditProfilePanelService editProfilePanelService)
    {
        _roomUserDataService = roomUserDataService;
        _editProfilePanelService = editProfilePanelService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "set_profile_label_color_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isClearCommand = context.Command is "removeprofilelabelcolor" or "remove-profile-label-color"
                                               or "clearprofilelabelcolor" or "clear-profile-label-color";
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

        if (!string.IsNullOrEmpty(colorKey) && !EditProfilePanelCommand.PROFILE_LABEL_COLORS.ContainsKey(colorKey))
        {
            context.ReplyLocalizedMessage("set_profile_label_color_invalid",
                string.Join(", ", EditProfilePanelCommand.PROFILE_LABEL_COLORS.Keys));
            return;
        }

        var colorValue = string.IsNullOrEmpty(colorKey)
            ? string.Empty
            : EditProfilePanelCommand.PROFILE_LABEL_COLORS[colorKey];

        try
        {
            await _roomUserDataService.SetUserLabelColorAsync(
                roomId, context.Sender.UserId, colorValue, cancellationToken);
            context.ReplyLocalizedMessage("set_profile_label_color_success");
            await _editProfilePanelService.SendPanelAsync(context, roomId, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating profile label color");
            context.ReplyLocalizedMessage("set_profile_label_color_failure", exception.Message);
        }
    }
}
