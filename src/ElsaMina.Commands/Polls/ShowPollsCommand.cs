using ElsaMina.Commands.Polls.ShowPolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Polls;

[NamedCommand("showpolls", Aliases = ["pollhistory", "poll-history"])]
public class ShowPollsCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public ShowPollsCommand(IRoomsManager roomsManager,
        IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _roomsManager = roomsManager;
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    private const int PAGE_SIZE = 10;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",", 2);
        var roomIdPart = parts[0].Trim();
        var page = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsedPage)
            ? Math.Max(1, parsedPage)
            : 1;

        string roomId;
        if (!string.IsNullOrWhiteSpace(roomIdPart))
        {
            roomId = roomIdPart.ToLowerAlphaNum();
            if (!_roomsManager.HasRoom(roomId))
            {
                context.ReplyRankAwareLocalizedMessage("show_polls_room_not_exist", roomIdPart);
                return;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var totalPolls = await dbContext.SavedPolls
            .CountAsync(poll => poll.RoomId == roomId, cancellationToken);

        if (totalPolls == 0)
        {
            context.ReplyRankAwareLocalizedMessage("show_polls_no_polls", roomId);
            return;
        }

        var totalPages = (int)Math.Ceiling(totalPolls / (double)PAGE_SIZE);
        page = Math.Clamp(page, 1, totalPages);

        var polls = await dbContext.SavedPolls
            .Where(poll => poll.RoomId == roomId)
            .OrderByDescending(poll => poll.EndedAt)
            .Skip((page - 1) * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var viewModel = new ShowPollsViewModel
        {
            Culture = context.Culture,
            RoomId = roomId,
            RoomName = room?.Name ?? roomId,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            Polls = polls,
            Page = page,
            TotalPages = totalPages,
            TimeZone = context.Room?.TimeZone ?? TimeZoneInfo.Utc
        };

        var template = await _templatesManager.GetTemplateAsync("Polls/ShowPolls/ShowPolls", viewModel);
        context.ReplyHtmlPage("polls-history",
            template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags());
    }
}