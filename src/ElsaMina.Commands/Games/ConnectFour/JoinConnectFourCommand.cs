using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.ConnectFour;

[NamedCommand("c4join", Aliases = ["c4j"])]
public class JoinConnectFourCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public JoinConnectFourCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(context.Target);
        if (room?.Game is not IConnectFourGame connectFour)
        {
            return;
        }

        await connectFour.JoinGame(context.Sender);
        if (!connectFour.IsStarted)
        {
            await connectFour.DisplayAnnounce(); // Gets updated
        }
    }
}