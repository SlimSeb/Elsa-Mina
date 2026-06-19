using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

[NamedCommand("chessplay", Aliases = ["chessmove"])]
public class PlayChessCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public PlayChessCommand(IRoomsManager roomsManager)
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
        var play = parts[1].Trim();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IChessGame chess)
        {
            await chess.Play(context.Sender, play);
        }
    }
}
