using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.LightsOut;

[NamedCommand("lojoin")]
public class JoinLightsOutCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public JoinLightsOutCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is not ILightsOutGame lightsOut || lightsOut.IsStarted)
        {
            return;
        }

        lightsOut.Owner = context.Sender;
        await lightsOut.StartNewRound();
    }
}
