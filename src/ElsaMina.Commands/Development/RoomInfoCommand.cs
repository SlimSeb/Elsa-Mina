using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.RoomInfo;
using Newtonsoft.Json;

namespace ElsaMina.Commands.Development;

[NamedCommand("roominfo", Aliases = ["roominfotest"])]
public class RoomInfoCommand : Command
{
    public override bool IsAllowedInPrivateMessage => true;

    private readonly IRoomInfoManager _roomInfoManager;

    public RoomInfoCommand(IRoomInfoManager roomInfoManager)
    {
        _roomInfoManager = roomInfoManager;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target) ? context.RoomId : context.Target;
        if (string.IsNullOrWhiteSpace(roomId))
        {
            context.ReplyLocalizedMessage("roominfo_no_room");
            return;
        }

        var roomInfo = await _roomInfoManager.GetRoomInfoAsync(roomId, cancellationToken);

        if (roomInfo == null)
        {
            context.ReplyLocalizedMessage("roominfo_timeout");
            return;
        }

        if (!string.IsNullOrEmpty(roomInfo.Error))
        {
            context.ReplyLocalizedMessage("roominfo_error", roomInfo.Error);
            return;
        }

        context.Reply($"!code {JsonConvert.SerializeObject(roomInfo, Formatting.Indented)}");
    }
}
