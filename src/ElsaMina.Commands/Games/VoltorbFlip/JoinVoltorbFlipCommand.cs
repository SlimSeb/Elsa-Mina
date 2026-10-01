using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("vfjoin")]
public class JoinVoltorbFlipCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public JoinVoltorbFlipCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is not IVoltorbFlipGame voltorbFlip || voltorbFlip.IsStarted)
        {
            return;
        }

        voltorbFlip.Owner = context.Sender;
        await voltorbFlip.StartNewRound();
    }
}
