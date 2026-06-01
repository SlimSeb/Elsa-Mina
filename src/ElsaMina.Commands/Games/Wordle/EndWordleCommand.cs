using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

[NamedCommand("wlend", Aliases = ["end-wordle"])]
public class EndWordleCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IWordleGameManager _gameManager;

    public EndWordleCommand(IRoomsManager roomsManager, IWordleGameManager gameManager)
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

            var wordle = _gameManager.GetGame(roomId, context.Sender.UserId);
            if (wordle == null)
            {
                context.ReplyLocalizedMessage("wordle_game_no_game");
                return;
            }

            wordle.Context = context;
            await wordle.CancelAsync();
            context.ReplyLocalizedMessage("wordle_game_cancelled");
            return;
        }

        var roomGame = _gameManager.GetGame(context.RoomId, context.Sender.UserId);
        if (roomGame == null)
        {
            context.ReplyLocalizedMessage("wordle_game_no_game");
            return;
        }

        await roomGame.CancelAsync();
        context.ReplyLocalizedMessage("wordle_game_cancelled");
    }
}
