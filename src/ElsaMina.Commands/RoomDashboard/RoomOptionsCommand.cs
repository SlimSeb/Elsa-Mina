using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("room-options", Aliases = ["roomoptions", "roptions"])]
public class RoomOptionsCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IRoomDashboardService _roomDashboardService;

    public RoomOptionsCommand(
        IRoomsManager roomsManager,
        IRoomDashboardService roomDashboardService)
    {
        _roomsManager = roomsManager;
        _roomDashboardService = roomDashboardService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target)
            ? context.RoomId
            : context.Target.Trim().ToLowerAlphaNum();

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

        if (!await context.HasSufficientRankInRoom(roomId, Rank.Driver, cancellationToken))
        {
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        await _roomDashboardService.SendOptionsPageAsync(context, roomId, cancellationToken);
    }
}
