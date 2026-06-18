using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Battleship;

[NamedCommand("bselo", Aliases = ["bsrating"])]
public class BattleshipEloCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public BattleshipEloCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var userId = string.IsNullOrWhiteSpace(context.Target)
            ? context.Sender.UserId
            : context.Target.Trim().ToLowerInvariant().Replace(" ", "");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rating = await dbContext.BattleshipRatings
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (rating is null)
        {
            context.ReplyLocalizedMessage("battleship_elo_not_found", userId);
            return;
        }

        context.ReplyLocalizedMessage("battleship_elo_info", rating.UserId, rating.Rating, rating.Wins,
            rating.Losses);
    }
}
