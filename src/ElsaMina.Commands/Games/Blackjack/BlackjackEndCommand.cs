using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("bjend", Aliases = ["end-bj"])]
public class BlackjackEndCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IBlackjackGameManager _gameManager;

    public BlackjackEndCommand(IRoomsManager roomsManager, IBlackjackGameManager gameManager)
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
                context.ReplyLocalizedMessage("bj_game_no_game");
                return;
            }

            game.Context = context;
            await game.CancelAsync();
            context.ReplyLocalizedMessage("bj_game_cancelled");
            return;
        }

        if (context.Room?.Game is IBlackjackGame roomGame)
        {
            if (roomGame.Owner != null && context.Sender.UserId != roomGame.Owner.UserId
                                       && !context.HasRankOrHigher(Rank.Driver))
            {
                context.ReplyLocalizedMessage("bj_game_not_owner");
                return;
            }

            await roomGame.CancelAsync();
            context.ReplyLocalizedMessage("bj_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("bj_game_no_game");
        }
    }
}
