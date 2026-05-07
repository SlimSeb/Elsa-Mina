using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.History;

[NamedCommand("tourhistory", Aliases = ["tourhistorique", "historialtour"])]
public class TourHistoryCommand : Command
{
    private const int MAX_ENTRIES = 30;

    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IRoomsManager _roomsManager;

    public TourHistoryCommand(IBotDbContextFactory botDbContextFactory, ITemplatesManager templatesManager,
        IRoomsManager roomsManager)
    {
        _botDbContextFactory = botDbContextFactory;
        _templatesManager = templatesManager;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        if (!string.IsNullOrEmpty(context.Target))
        {
            roomId = context.Target.ToLowerAlphaNum();
            if (!_roomsManager.HasRoom(roomId))
            {
                context.ReplyLocalizedMessage("tour_history_room_not_found", context.Target);
                return;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.SavedTournaments
            .Where(t => t.RoomId == roomId)
            .OrderByDescending(t => t.EndedAt)
            .Take(MAX_ENTRIES)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            context.ReplyLocalizedMessage("tour_history_no_history", roomId);
            return;
        }

        var timeZone = context.Room?.TimeZone ?? TimeZoneInfo.Local;
        var entries = records
            .Select(t => new TourHistoryEntry(
                Id: t.Id,
                Format: t.Format,
                Winner: t.Winner,
                RunnerUp: t.RunnerUp,
                SemiFinalists: string.IsNullOrEmpty(t.SemiFinalists)
                    ? []
                    : t.SemiFinalists.Split(',', StringSplitOptions.RemoveEmptyEntries),
                PlayerCount: t.PlayerCount,
                EndedAt: TimeZoneInfo.ConvertTime(t.EndedAt, timeZone).ToString("g", context.Culture)))
            .ToList();

        var roomLabel = _roomsManager.GetRoom(roomId)?.Name ?? roomId;
        var template = await _templatesManager.GetTemplateAsync("Tournaments/History/TourHistory",
            new TourHistoryViewModel
            {
                Culture = context.Culture,
                Room = roomLabel,
                Entries = entries
            });

        context.ReplyHtml(template.RemoveNewlines().RemoveWhitespacesBetweenTags(), rankAware: true);
    }
}
