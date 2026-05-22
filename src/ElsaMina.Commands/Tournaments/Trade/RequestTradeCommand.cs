using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Trade;

[NamedCommand("requesttrade")]
public class RequestTradeCommand : Command
{
    private const string STAFF_ROOM = "frenchstaff";

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;

    public RequestTradeCommand(IBotDbContextFactory dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "requesttrade_help";

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

        var roomId = parts.Length >= 4 ? parts[3].Trim().ToLowerAlphaNum() : context.RoomId;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var originRecord = await dbContext.TournamentRecords
            .FirstOrDefaultAsync(record => record.UserId == originUserId && record.RoomId == roomId, cancellationToken);

        if (originRecord == null)
        {
            context.ReplyLocalizedMessage("tour_points_not_found", parts[0].Trim(), roomId);
            return;
        }

        if (originRecord.WinsCount < points)
        {
            context.ReplyLocalizedMessage("tradepoints_not_enough", parts[0].Trim());
            return;
        }

        var trigger = _configuration.Trigger;
        var approveButton = $"""<button class="button" name="send" value="{trigger}tradepoints {originUserId}, {newUserId}, {points}, {roomId}">Approuver</button>""";
        var disapproveButton = $"""<button class="button" name="send" value="{trigger}notrade {originUserId}, {points}">Refuser</button>""";

        context.ReplyLocalizedMessage("requesttrade_created",
            points, parts[0].Trim(), parts[1].Trim(), roomId);

        var staffMessage = context.GetString("requesttrade_staff_message",
            points, parts[0].Trim(), parts[1].Trim(), roomId, context.Sender.Name);

        context.SendMessageIn(STAFF_ROOM,
            $"/adduhtml trade-req-{originUserId}-{points}, {staffMessage} {approveButton} {disapproveButton}");
    }
}
