using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("vfquit")]
public class QuitVoltorbFlipCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly IVoltorbFlipGameManager _gameManager;

    public QuitVoltorbFlipCommand(IRoomsManager roomsManager, IVoltorbFlipGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();

        var voltorbFlip = _gameManager.GetGame(roomId, context.Sender.UserId)
            ?? _roomsManager.GetRoom(roomId)?.Game as IVoltorbFlipGame;

        if (voltorbFlip != null)
        {
            if (voltorbFlip.IsPrivateMode)
            {
                var room = _roomsManager.GetRoom(roomId);
                if (room != null) context.Culture = room.Culture;
                voltorbFlip.Context = context;
            }
            await voltorbFlip.QuitRound(context.Sender);
        }
    }
}
