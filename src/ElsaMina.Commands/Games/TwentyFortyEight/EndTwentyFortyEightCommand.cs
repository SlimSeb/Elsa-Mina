using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

[NamedCommand("2048end", Aliases = ["end-2048"])]
public class EndTwentyFortyEightCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITwentyFortyEightGameManager _gameManager;

    public EndTwentyFortyEightCommand(IRoomsManager roomsManager, ITwentyFortyEightGameManager gameManager)
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

            var game = _gameManager.GetGame(roomId, context.Sender.UserId);
            if (game == null)
            {
                context.ReplyLocalizedMessage("tfe_game_no_game");
                return;
            }

            game.Context = context;
            await game.CancelAsync();
            context.ReplyLocalizedMessage("tfe_game_cancelled");
            return;
        }

        if (context.Room?.Game is ITwentyFortyEightGame roomGame)
        {
            if (roomGame.Owner != null && context.Sender.UserId != roomGame.Owner.UserId
                                       && !context.HasRankOrHigher(Rank.Driver))
            {
                context.ReplyLocalizedMessage("tfe_game_not_owner");
                return;
            }

            await roomGame.CancelAsync();
            context.ReplyLocalizedMessage("tfe_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("tfe_game_no_game");
        }
    }
}
