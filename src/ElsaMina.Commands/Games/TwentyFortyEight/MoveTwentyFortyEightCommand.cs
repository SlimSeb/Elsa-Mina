using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

[NamedCommand("2048move")]
public class MoveTwentyFortyEightCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITwentyFortyEightGameManager _gameManager;

    public MoveTwentyFortyEightCommand(IRoomsManager roomsManager, ITwentyFortyEightGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
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
        var direction = parts[1].Trim();

        var game = _gameManager.GetGame(roomId, context.Sender.UserId)
            ?? _roomsManager.GetRoom(roomId)?.Game as ITwentyFortyEightGame;

        if (game != null)
        {
            if (game.IsPrivateMode)
            {
                var room = _roomsManager.GetRoom(roomId);
                if (room != null) context.Culture = room.Culture;
                game.Context = context;
            }

            await game.MakeMove(context.Sender, direction);
        }
    }
}
