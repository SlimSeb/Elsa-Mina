using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Badges.BadgeHolders;

[NamedCommand("badgeholders", "badge-holders")]
public class BadgeHoldersCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    public BadgeHoldersCommand(ITemplatesManager templatesManager, IBotDbContextFactory dbContextFactory)
    {
        _templatesManager = templatesManager;
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "badgeholders_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target) ? context.RoomId : context.Target;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var badges = await dbContext.Badges
            .Include(badge => badge.BadgeHolders)
            .ThenInclude(badgeHolding => badgeHolding.RoomUser)
            .ThenInclude(roomUser => roomUser.User)
            .Where(badge => badge.RoomId == roomId)
            .OrderBy(badge => badge.Name)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var viewModel = new BadgeHoldersViewModel
        {
            Culture = context.Culture,
            Badges = badges
        };
        var template = await _templatesManager.GetTemplateAsync("Badges/BadgeHolders/BadgeHolders", viewModel);
        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}