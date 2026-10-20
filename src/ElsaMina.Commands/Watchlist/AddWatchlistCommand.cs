using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Watchlist;

[NamedCommand("addwatchlist")]
public class AddWatchlistCommand : Command
{
    private readonly IWatchlistService _watchlistService;

    public AddWatchlistCommand(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    // Format : room, user, rank
    public override string GetTargetRoomId(IContext context) => context.Target.Split(',', 3)[0].Trim();
    public override string HelpMessageKey => "watchlist_add_help";

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

        await _watchlistService.AddToWatchlistAsync(roomId, user, rank, cancellationToken);
        context.ReplyLocalizedMessage("watchlist_user_added", user, roomId, rank);

        await _watchlistService.SendDiscordNotificationAsync(roomId,
            $"{context.Sender.Name} a ajouté {user} à la watchlist avec le rang {rank}.", cancellationToken);

        await _watchlistService.FetchAndUpdateStaffIntroAsync(roomId, cancellationToken);
    }
}
