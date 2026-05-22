using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("vfend", Aliases = ["end-voltorbflip"])]
public class EndVoltorbFlipCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IVoltorbFlipGameManager _gameManager;

    public EndVoltorbFlipCommand(IRoomsManager roomsManager, IVoltorbFlipGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsPrivateMessage)
        {
            await HandlePrivateMessageAsync(context);
            return;
        }

        await HandleRoomMessageAsync(context);
    }

    private async Task HandlePrivateMessageAsync(IContext context)
    {
        var roomId = context.Target?.Trim();
        if (string.IsNullOrEmpty(roomId))
        {
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room != null)
        {
            context.Culture = room.Culture;
        }

        var voltorbFlip = _gameManager.GetGame(roomId, context.Sender.UserId);
        if (voltorbFlip == null)
        {
            context.ReplyLocalizedMessage("vf_game_no_game");
            return;
        }

        voltorbFlip.Context = context;
        await voltorbFlip.CancelAsync();
        context.ReplyLocalizedMessage("vf_game_cancelled");
    }

    private static async Task HandleRoomMessageAsync(IContext context)
    {
        if (context.Room?.Game is IVoltorbFlipGame voltorbFlip)
        {
            if (voltorbFlip.Owner != null && context.Sender.UserId != voltorbFlip.Owner.UserId
                                          && !context.HasRankOrHigher(Rank.Driver))
            {
                context.ReplyLocalizedMessage("vf_game_not_owner");
                return;
            }

            await voltorbFlip.CancelAsync();
            context.ReplyLocalizedMessage("vf_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("vf_game_no_game");
        }
    }
}
