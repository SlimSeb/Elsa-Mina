using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Watchlist;

[NamedCommand("removewatchlist")]
public class RemoveWatchlistCommand : Command
{
    private readonly IWatchlistService _watchlistService;

    public RemoveWatchlistCommand(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    // Format : room, user, rank
    public override string GetTargetRoomId(IContext context) => context.Target.Split(',', 3)[0].Trim();
    public override string HelpMessageKey => "watchlist_remove_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var args = context.Target.Split(',', 3);
        if (args.Length != 3)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var roomId = GetTargetRoomId(context);
        var user = args[1].Trim();
        var rank = args[2].Trim();

        var removed = await _watchlistService.RemoveFromWatchlistAsync(roomId, user, rank, cancellationToken);
        if (!removed)
        {
            context.ReplyLocalizedMessage("watchlist_user_not_found", user, rank, roomId);
            return;
        }

        context.ReplyLocalizedMessage("watchlist_user_removed", user, rank, roomId);

        await _watchlistService.SendDiscordNotificationAsync(roomId,
            $"{context.Sender.Name} a retiré {user} de la watchlist.", cancellationToken);

        await _watchlistService.FetchAndUpdateStaffIntroAsync(roomId, cancellationToken);
    }
}
