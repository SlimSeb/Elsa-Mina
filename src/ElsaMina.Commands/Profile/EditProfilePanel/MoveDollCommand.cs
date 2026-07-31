using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

[NamedCommand("movedoll", Aliases = ["move-doll", "reorderdoll"])]
public class MoveDollCommand : Command
{
    private readonly IDollService _dollService;
    private readonly IEditProfilePanelService _editProfilePanelService;

    public MoveDollCommand(IDollService dollService, IEditProfilePanelService editProfilePanelService)
    {
        _dollService = dollService;
        _editProfilePanelService = editProfilePanelService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "move_doll_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length < 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var dollId = parts[0].ToLowerAlphaNum();
        var offset = GetOffset(parts[1]);
        if (offset == null)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        string roomId;
        if (context.IsPrivateMessage)
        {
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            {
                context.ReplyLocalizedMessage("move_doll_missing_room");
                return;
            }

            roomId = parts[2].Trim().ToLowerAlphaNum();
        }
        else
        {
            roomId = context.RoomId;
        }

        DollMoveResult result;
        try
        {
            result = await _dollService.MoveDollAsync(roomId, context.Sender.UserId, dollId, offset.Value,
                cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while moving a doll");
            context.ReplyLocalizedMessage("move_doll_failure", exception.Message);
            return;
        }

        switch (result)
        {
            case DollMoveResult.NotOwned:
                context.ReplyLocalizedMessage("move_doll_not_owned", dollId);
                return;
            case DollMoveResult.AlreadyAtEdge:
                context.ReplyLocalizedMessage("move_doll_at_edge");
                return;
        }

        // The panel is the only place the arrows live, so refreshing it is the whole feedback :
        // the dolls move under the user's cursor without spamming the chat.
        await _editProfilePanelService.SendPanelAsync(context, roomId, cancellationToken);
    }

    private static int? GetOffset(string direction) => direction.Trim().ToLowerInvariant() switch
    {
        "left" or "l" => -1,
        "right" or "r" => 1,
        _ => null
    };
}
