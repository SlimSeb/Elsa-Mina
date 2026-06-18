using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

[NamedCommand("bsjoin", Aliases = ["bsj"])]
public class JoinBattleshipCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public JoinBattleshipCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(context.Target);
        if (room?.Game is not IBattleshipGame battleship)
        {
            return;
        }

        await battleship.JoinGame(context.Sender);
    }
}
