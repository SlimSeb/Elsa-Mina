using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.ConnectFour;

[NamedCommand("c4forfeit", "c4ff")]
public class ForfeitConnectFourCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public ForfeitConnectFourCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IConnectFourGame connectFour)
        {
            await connectFour.Forfeit(context.Sender);
        }
    }
}