using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("vfflip")]
public class FlipVoltorbFlipCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly IVoltorbFlipGameManager _gameManager;

    public FlipVoltorbFlipCommand(IRoomsManager roomsManager, IVoltorbFlipGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length < 3)
        {
            return;
        }

        var roomId = parts[0].Trim();
        if (!int.TryParse(parts[1].Trim(), out var row))
        {
            return;
        }

        if (!int.TryParse(parts[2].Trim(), out var col))
        {
            return;
        }

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
            await voltorbFlip.FlipTile(context.Sender, row, col);
        }
    }
}
