using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

[NamedCommand("bsplace", Aliases = ["bsp"])]
public class PlaceBattleshipCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public PlaceBattleshipCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
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
        var coordinate = parts[1].Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IBattleshipGame battleship)
        {
            await battleship.PlaceShip(context.Sender, coordinate);
        }
    }
}
