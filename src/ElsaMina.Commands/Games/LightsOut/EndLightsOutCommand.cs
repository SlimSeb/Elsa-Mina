using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.LightsOut;

[NamedCommand("loend", Aliases = ["end-lightsout"])]
public class EndLightsOutCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly ILightsOutGameManager _gameManager;

    public EndLightsOutCommand(IRoomsManager roomsManager, ILightsOutGameManager gameManager)
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

            var lightsOut = _gameManager.GetGame(roomId, context.Sender.UserId);
            if (lightsOut == null)
            {
                context.ReplyLocalizedMessage("lo_game_no_game");
                return;
            }

            lightsOut.Context = context;
            await lightsOut.CancelAsync();
            context.ReplyLocalizedMessage("lo_game_cancelled");
            return;
        }

        if (context.Room?.Game is ILightsOutGame game)
        {
            if (game.Owner != null && context.Sender.UserId != game.Owner.UserId
                                   && !context.HasRankOrHigher(Rank.Driver))
            {
                context.ReplyLocalizedMessage("lo_game_not_owner");
                return;
            }

            await game.CancelAsync();
            context.ReplyLocalizedMessage("lo_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("lo_game_no_game");
        }
    }
}