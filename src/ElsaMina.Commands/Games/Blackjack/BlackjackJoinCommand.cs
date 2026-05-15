using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("bjjoin")]
public class BlackjackJoinCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public BlackjackJoinCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target?.Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is not IBlackjackGame game || game.IsStarted)
        {
            return;
        }

        game.Owner = context.Sender;
        game.Context = context;
        await game.StartGame();
    }
}
