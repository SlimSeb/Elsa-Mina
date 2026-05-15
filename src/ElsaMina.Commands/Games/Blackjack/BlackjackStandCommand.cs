using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("bjstand")]
public class BlackjackStandCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IBlackjackGameManager _gameManager;

    public BlackjackStandCommand(IRoomsManager roomsManager, IBlackjackGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target?.Trim();
        if (string.IsNullOrEmpty(roomId))
        {
            return;
        }

        var game = _gameManager.GetGame(roomId, context.Sender.UserId)
            ?? _roomsManager.GetRoom(roomId)?.Game as IBlackjackGame;

        if (game == null)
        {
            return;
        }

        if (game.IsPrivateMode)
        {
            var room = _roomsManager.GetRoom(roomId);
            if (room != null) context.Culture = room.Culture;
            game.Context = context;
        }

        await game.Stand(context.Sender);
    }
}
