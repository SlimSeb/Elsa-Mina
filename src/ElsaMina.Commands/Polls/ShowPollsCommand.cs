using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls;

[NamedCommand("showpolls")]
public class ShowPollsCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    public ShowPollsCommand(IRoomsManager roomsManager, IBotDbContextFactory dbContextFactory)
    {
        _roomsManager = roomsManager;
        _dbContextFactory = dbContextFactory;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        if (!string.IsNullOrEmpty(context.Target))
        {
            roomId = context.Target.ToLower();
            if (!_roomsManager.HasRoom(roomId))
            {
                context.ReplyRankAwareLocalizedMessage("show_polls_room_not_exist", roomId);
                return;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pollHistory = await dbContext.SavedPolls
            .Where(poll => poll.RoomId == roomId)
            .ToListAsync(cancellationToken);

        if (pollHistory.Count == 0)
        {
            context.ReplyRankAwareLocalizedMessage("show_polls_no_polls", roomId);
            return;
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.Append(context.GetString("show_polls_history_header", roomId));
        foreach (var poll in pollHistory)
        {
            stringBuilder.AppendLine(context.GetString("show_polls_history_entry",
                poll.Id,
                TimeZoneInfo.ConvertTime(poll.EndedAt, context.Room.TimeZone).ToString("G", context.Culture),
                poll.Content));
        }

        context.ReplyRankAwareLocalizedMessage("show_polls_history_sent");
        context.ReplyHtmlPage("polls-history", stringBuilder.ToString());
    }
}