using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("vftogglemark")]
public class ToggleMarkVoltorbFlipCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly IVoltorbFlipGameManager _gameManager;

    public ToggleMarkVoltorbFlipCommand(IRoomsManager roomsManager, IVoltorbFlipGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        var roomId = parts[0].Trim();

        if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var markerTypeInt)
            || !Enum.IsDefined(typeof(VoltorbFlipMarkerType), markerTypeInt)
            || markerTypeInt == (int)VoltorbFlipMarkerType.None)
        {
            return;
        }

        var markerType = (VoltorbFlipMarkerType)markerTypeInt;

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
            await voltorbFlip.SetMarkerType(context.Sender, markerType);
        }
    }
}
