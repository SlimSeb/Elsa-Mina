using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Badges.BadgeEditPanel;

[NamedCommand("badge-edit-panel", "badgeeditpanel", "editbadges", "updatebadges", "badgepanel")]
public class BadgeEditPanelCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;

    public BadgeEditPanelCommand(IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRoomsManager roomsManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "badge_edit_panel_help_message";

    private const int PAGE_SIZE = 10;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",", 2);
        var roomIdPart = parts[0].Trim();
        var roomId = string.IsNullOrWhiteSpace(roomIdPart)
            ? context.RoomId
            : roomIdPart.ToLowerAlphaNum();
        var page = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsedPage)
            ? Math.Max(1, parsedPage)
            : 1;

        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var totalBadges = await dbContext.Badges.CountAsync(badge => badge.RoomId == roomId, cancellationToken);

        if (totalBadges == 0)
        {
            context.ReplyLocalizedMessage("badge_edit_panel_no_badges", room?.Name ?? roomId);
            return;
        }

        var totalPages = (int)Math.Ceiling(totalBadges / (double)PAGE_SIZE);
        page = Math.Clamp(page, 1, totalPages);

        var badges = await dbContext.Badges
            .Where(badge => badge.RoomId == roomId)
            .OrderBy(badge => badge.Name)
            .Skip((page - 1) * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .ToListAsync(cancellationToken);

        var viewModel = new BadgeEditPanelViewModel
        {
            Culture = context.Culture,
            RoomId = roomId,
            RoomName = room?.Name ?? roomId,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            EditCommand = $"/w {_configuration.Name},{_configuration.Trigger}editbadge {{badgeId}}, {{name}}, {{image}}, {{trophy}}, {roomId}",
            Badges = badges,
            Page = page,
            TotalPages = totalPages
        };

        var template = await _templatesManager.GetTemplateAsync("Badges/BadgeEditPanel/BadgeEditPanel", viewModel);
        context.ReplyHtmlPage($"badge-edit-{roomId}", template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags());
    }
}
