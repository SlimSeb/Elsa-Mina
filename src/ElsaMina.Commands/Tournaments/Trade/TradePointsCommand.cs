using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Trade;

[NamedCommand("tradepoints")]
public class TradePointsCommand : Command
{
    private const string STAFF_ROOM = "frenchstaff";

    private readonly IBotDbContextFactory _dbContextFactory;

    public TradePointsCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "tradepoints_help";

    // Format : origine, destinataire, points[, room]
    public override string GetTargetRoomId(IContext context) => context.GetRoomIdArgument(3) ?? context.RoomId;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var parts = context.Target.Split(',');
        if (parts.Length < 3)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var originUserId = parts[0].Trim().ToLowerAlphaNum();
        var newUserId = parts[1].Trim().ToLowerAlphaNum();
        if (!int.TryParse(parts[2].Trim(), out var points) || points <= 0)
        {
            context.ReplyLocalizedMessage("tradepoints_invalid_points");
            return;
        }

        var roomId = GetTargetRoomId(context);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var originRecord = await dbContext.TournamentRecords
            .FirstOrDefaultAsync(record => record.UserId == originUserId && record.RoomId == roomId, cancellationToken);

        if (originRecord == null || originRecord.WinsCount < points)
        {
            context.ReplyLocalizedMessage("tradepoints_not_enough", parts[0].Trim());
            return;
        }

        var newRecord = await dbContext.TournamentRecords
            .FirstOrDefaultAsync(record => record.UserId == newUserId && record.RoomId == roomId, cancellationToken);

        if (newRecord == null)
        {
            newRecord = new DataAccess.Models.TournamentRecord
            {
                UserId = newUserId,
                RoomId = roomId
            };
            await dbContext.TournamentRecords.AddAsync(newRecord, cancellationToken);
        }

        originRecord.WinsCount -= points;
        newRecord.WinsCount += points;
        await dbContext.SaveChangesAsync(cancellationToken);

        context.ReplyLocalizedMessage("tradepoints_success", points, parts[0].Trim(), parts[1].Trim());
        context.SendMessageIn(STAFF_ROOM,
            $"/adduhtml trade-{originUserId}-{points}, " +
            context.GetString("tradepoints_staff_processed", parts[0].Trim(), parts[1].Trim(), points, roomId));
    }
}
