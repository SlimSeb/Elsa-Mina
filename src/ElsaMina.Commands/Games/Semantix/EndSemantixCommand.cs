using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Semantix;

[NamedCommand("sxend", Aliases = ["end-semantix"])]
public class EndSemantixCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly ISemantixGameManager _gameManager;

    public EndSemantixCommand(IRoomsManager roomsManager, ISemantixGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        if (context.IsPrivateMessage)
        {
            roomId = context.Target?.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                return;
            }

            var room = _roomsManager.GetRoom(roomId);
            if (room != null)
            {
                context.Culture = room.Culture;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        var semantix = _gameManager.GetGame(roomId, context.Sender.UserId);
        if (semantix == null)
        {
            context.ReplyLocalizedMessage("sx_game_no_game");
            return;
        }

        if (semantix.IsPrivateMode)
        {
            semantix.Context = context;
        }

        await semantix.CancelAsync();
        context.ReplyLocalizedMessage("sx_game_cancelled");
    }
}
