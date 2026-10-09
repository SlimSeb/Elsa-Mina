using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

[NamedCommand("2048join")]
public class JoinTwentyFortyEightCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public JoinTwentyFortyEightCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is not ITwentyFortyEightGame game || game.IsStarted)
        {
            return;
        }

        game.Owner = context.Sender;
        await game.StartNewRound();
    }
}
