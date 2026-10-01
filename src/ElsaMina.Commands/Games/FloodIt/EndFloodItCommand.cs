using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.FloodIt;

[NamedCommand("fiend", Aliases = ["end-floodit"])]
public class EndFloodItCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly IFloodItGameManager _gameManager;

    public EndFloodItCommand(IRoomsManager roomsManager, IFloodItGameManager gameManager)
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
            var roomId = context.Target?.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                return;
            }

            var room = _roomsManager.GetRoom(roomId);
            if (room != null) context.Culture = room.Culture;

            var floodIt = _gameManager.GetGame(roomId, context.Sender.UserId);
            if (floodIt == null)
            {
                context.ReplyLocalizedMessage("fi_game_no_game");
                return;
            }

            floodIt.Context = context;
            await floodIt.CancelAsync();
            context.ReplyLocalizedMessage("fi_game_cancelled");
            return;
        }

        if (context.Room?.Game is IFloodItGame game)
        {
            if (game.Owner != null && context.Sender.UserId != game.Owner.UserId
                                   && !context.HasRankOrHigher(Rank.Driver))
            {
                context.ReplyLocalizedMessage("fi_game_not_owner");
                return;
            }

            await game.CancelAsync();
            context.ReplyLocalizedMessage("fi_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("fi_game_no_game");
        }
    }
}
