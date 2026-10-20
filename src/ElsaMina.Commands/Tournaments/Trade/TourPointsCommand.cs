using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Trade;

[NamedCommand("lbpoints", Aliases = ["boardpoints"])]
public class TourPointsCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public TourPointsCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRankInPrivateMessage => Rank.Regular;
    public override string HelpMessageKey => "tour_points_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var parts = context.Target.Split(',');
        var userId = parts[0].Trim().ToLowerAlphaNum();
        var roomId = parts.Length >= 2 ? parts[1].Trim().ToLowerAlphaNum() : context.RoomId;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.TournamentRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.RoomId == roomId, cancellationToken);

        if (record == null)
        {
            context.ReplyLocalizedMessage("tour_points_not_found", parts[0].Trim(), roomId);
            return;
        }

        context.ReplyLocalizedMessage("tour_points_result", parts[0].Trim(), record.WinsCount, roomId);
    }
}
