using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Badges.BadgeList;

[NamedCommand("badgelist", Aliases = ["badges", "allbadges"])]
public class BadgeListCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public BadgeListCommand(IRoomsManager roomsManager, IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager)
    {
        _roomsManager = roomsManager;
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "badgelist_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target)
            ? context.RoomId
            : context.Target.ToLowerAlphaNum();

        if (!_roomsManager.HasRoom(roomId))
        {
            context.ReplyLocalizedMessage("badgelist_room_not_found", roomId);
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var badges = await dbContext.Badges
            .Where(badge => badge.RoomId == roomId)
            .OrderBy(badge => badge.Name)
            .ToArrayAsync(cancellationToken);

        var viewModel = new BadgeListViewModel
        {
            Culture = context.Culture,
            Badges = badges
        };
        var template = await _templatesManager.GetTemplateAsync("Badges/BadgeList/BadgeList", viewModel);
        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}