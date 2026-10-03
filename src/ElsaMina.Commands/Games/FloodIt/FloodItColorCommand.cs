using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.FloodIt;

[NamedCommand("ficolor")]
public class FloodItColorCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly IFloodItGameManager _gameManager;

    public FloodItColorCommand(IRoomsManager roomsManager, IFloodItGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length < 2)
        {
            return;
        }

        var roomId = parts[0].Trim();
        if (!int.TryParse(parts[1].Trim(), out var colorIndex))
        {
            return;
        }

        var floodIt = _gameManager.GetGame(roomId, context.Sender.UserId)
            ?? _roomsManager.GetRoom(roomId)?.Game as IFloodItGame;

        if (floodIt != null)
        {
            if (floodIt.IsPrivateMode)
            {
                var room = _roomsManager.GetRoom(roomId);
                if (room != null) context.Culture = room.Culture;
                floodIt.Context = context;
            }

            await floodIt.FloodFill(context.Sender, colorIndex);
        }
    }
}
