using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Arcade.Points;

[NamedCommand("addp")]
public class AddPointsCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public AddPointsCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "add_points_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length != 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var user = parts[0].Trim().ToLowerAlphaNum();
        if (!double.TryParse(parts[1].Trim(), out var points))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userPoints = await dbContext.UserPoints.FindAsync([user], cancellationToken);

        if (userPoints == null)
        {
            await dbContext.EnsureUserExistsAsync(user, cancellationToken);
            userPoints = new DataAccess.Models.UserPoints
            {
                Id = user,
                Points = points
            };
            await dbContext.UserPoints.AddAsync(userPoints, cancellationToken);
        }
        else
        {
            userPoints.Points += points;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var allUserPoints = await dbContext.UserPoints
            .Where(up => up.Points > 0)
            .OrderByDescending(up => up.Points)
            .ToListAsync(cancellationToken);

        var template = await _templatesManager.GetTemplateAsync("Arcade/Points/PointsUpdate", new PointsUpdateViewModel
        {
            Culture = context.Culture,
            Username = user,
            PointsAdded = points,
            NewTotal = userPoints.Points,
            IsAddition = true,
            Leaderboard = allUserPoints.ToDictionary(up => up.Id, up => up.Points)
        });

        context.Reply($"/addhtmlbox {template.RemoveNewlines()}");
    }
}