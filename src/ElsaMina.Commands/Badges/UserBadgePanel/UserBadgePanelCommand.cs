using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Badges.UserBadgePanel;

[NamedCommand("userbadges", Aliases = ["user-badges", "managebadges"])]
public class UserBadgePanelCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;

    public UserBadgePanelCommand(IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRoomsManager roomsManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "user_badge_panel_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var parts = context.Target.Split(",");
        var targetUserId = parts[0].Trim().ToLowerAlphaNum();

        string roomId;
        if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            roomId = parts[1].Trim().ToLowerAlphaNum();
        }
        else if (!context.IsPrivateMessage)
        {
            roomId = context.RoomId;
        }
        else
        {
            context.ReplyLocalizedMessage("badge_pm_missing_room");
            return;
        }

        if (context.IsPrivateMessage && !await context.HasSufficientRankInRoom(roomId, Rank.Driver, cancellationToken))
        {
            context.ReplyLocalizedMessage("badge_pm_insufficient_rank");
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var allBadges = (await dbContext.Badges
                .Where(badge => badge.RoomId == roomId)
                .ToListAsync(cancellationToken))
            .OrderBy(badge => badge.Name, RomanNumeralSuffixComparer.INSTANCE)
            .ToList();

        var ownedBadgeIds = await dbContext.BadgeHoldings
            .Where(holding => holding.UserId == targetUserId && holding.RoomId == roomId)
            .Select(holding => holding.BadgeId)
            .ToListAsync(cancellationToken);

        var viewModel = new UserBadgePanelViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            RoomName = room?.Name ?? roomId,
            TargetUserId = targetUserId,
            AllBadges = allBadges,
            OwnedBadgeIds = new HashSet<string>(ownedBadgeIds)
        };

        var template = await _templatesManager.GetTemplateAsync("Badges/UserBadgePanel/UserBadgePanel", viewModel);
        context.ReplyHtmlPage($"userbadges-{roomId}-{targetUserId}",
            template.RemoveNewlines().CollapseAttributeWhitespace().RemoveWhitespacesBetweenTags());
    }
}
