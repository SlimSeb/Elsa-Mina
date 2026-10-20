using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("room-dashboard", Aliases = ["roomdashboard", "rdash"])]
public class ShowRoomDashboard : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IRoomDashboardService _roomDashboardService;

    public ShowRoomDashboard(
        IRoomsManager roomsManager,
        IRoomDashboardService roomDashboardService)
    {
        _roomsManager = roomsManager;
        _roomDashboardService = roomDashboardService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    public override string GetTargetRoomId(IContext context) => ParseArguments(context).RoomId;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var (roomId, isOptions) = ParseArguments(context);
        if (string.IsNullOrEmpty(roomId))
        {
            context.ReplyLocalizedMessage("dashboard_room_doesnt_exist", string.Empty);
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("dashboard_room_doesnt_exist", roomId);
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        if (isOptions)
        {
            await _roomDashboardService.SendOptionsPageAsync(context, roomId, cancellationToken);
        }
        else
        {
            await _roomDashboardService.SendDashboardPageAsync(context, roomId, cancellationToken);
        }
    }

    // La room est le 1er argument qui n'est pas "options", celle du message s'il y en a pas
    private static (string RoomId, bool IsOptions) ParseArguments(IContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return (context.RoomId, false);
        }

        var isOptions = false;
        string roomId = null;
        var parts = context.Target.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var cleanPart = part.ToLowerAlphaNum();
            if (cleanPart is "options" or "option" or "config")
            {
                isOptions = true;
            }
            else if (roomId == null)
            {
                roomId = cleanPart;
            }
        }

        return (roomId ?? context.RoomId, isOptions);
    }
}
