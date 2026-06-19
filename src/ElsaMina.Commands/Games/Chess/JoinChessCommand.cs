using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

[NamedCommand("chessjoin", Aliases = ["chessj"])]
public class JoinChessCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public JoinChessCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(context.Target);
        if (room?.Game is not IChessGame chess)
        {
            return;
        }

        await chess.JoinGame(context.Sender);
        if (!chess.IsStarted)
        {
            await chess.DisplayAnnounce(); // Gets updated
        }
    }
}
