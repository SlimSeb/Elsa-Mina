using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

[NamedCommand("bsforfeit", "bsff")]
public class ForfeitBattleshipCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public ForfeitBattleshipCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IBattleshipGame battleship)
        {
            await battleship.Forfeit(context.Sender);
        }
    }
}
