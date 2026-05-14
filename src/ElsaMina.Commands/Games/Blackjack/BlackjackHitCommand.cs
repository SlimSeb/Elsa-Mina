using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("bjhit")]
public class BlackjackHitCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public BlackjackHitCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
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

        var key = BlackjackCommand.GameKey(roomId, context.Sender.UserId);
        if (!BlackjackCommand.ACTIVE_GAMES.TryGetValue(key, out var game))
        {
            return;
        }

        await game.Hit();

        if (game.IsOver)
        {
            BlackjackCommand.ACTIVE_GAMES.TryRemove(key, out _);
        }
    }
}
