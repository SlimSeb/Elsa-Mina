using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Arcade.Levels;

[NamedCommand("displaypaliers", "displaypalier", "paliers", "arcadelevels")]
public class DisplayArcadeLevelsCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    public DisplayArcadeLevelsCommand(ITemplatesManager templatesManager, IBotDbContextFactory dbContextFactory)
    {
        _templatesManager = templatesManager;
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "display_paliers_help";
    public override string[] RoomRestriction => ["arcade", "botdevelopment"];

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<ArcadeLevelRow> rows;
        try
        {
            rows = await dbContext.ArcadeLevels
                .GroupJoin(
                    dbContext.Users,
                    arcadeLevel => arcadeLevel.Id, user => user.UserId,
                    (arcadeLevel, users) => new { ArcadeLevel = arcadeLevel, Users = users })
                .SelectMany(pair => pair.Users.DefaultIfEmpty(),
                    (pair, user) => new ArcadeLevelRow(pair.ArcadeLevel.Level, pair.ArcadeLevel.Id, user.UserName))
                .ToListAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while getting arcade levels.");
            return;
        }

        if (rows.Count == 0)
        {
            context.ReplyLocalizedMessage("arcade_level_no_users");
            return;
        }

        var levels = rows
            .GroupBy(row => row.Level)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new ArcadePlayer(row.UserId, row.UserName ?? row.UserId)).ToList()
            );

        var template = await _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", new ArcadeLevelsViewModel
        {
            Culture = context.Culture,
            Levels = levels
        });

        context.ReplyHtml(template.RemoveNewlines().CollapseAttributeWhitespace(), rankAware: true);
    }

    private sealed record ArcadeLevelRow(int Level, string UserId, string UserName);
}