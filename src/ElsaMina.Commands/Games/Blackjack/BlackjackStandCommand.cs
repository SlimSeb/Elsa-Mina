using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("bjstand")]
public class BlackjackStandCommand : Command
{
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

        await game.Stand();
        BlackjackCommand.ACTIVE_GAMES.TryRemove(key, out _);
    }
}
