using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Arcade.Points;

[NamedCommand("classement")]
public class LeaderboardCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public LeaderboardCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRankInPrivateMessage => Rank.Regular;
    public override string HelpMessageKey => "leaderboard_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var allUserPoints = await dbContext.UserPoints
            .Where(up => up.Points > 0)
            .OrderByDescending(up => up.Points)
            .ToListAsync(cancellationToken);

        if (allUserPoints.Count == 0)
        {
            context.ReplyLocalizedMessage("leaderboard_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Arcade/Points/Leaderboard", new LeaderboardViewModel
        {
            Culture = context.Culture,
            Leaderboard = allUserPoints.ToDictionary(up => up.Id, up => up.Points)
        });

        context.Reply($"/addhtmlbox {template.RemoveNewlines()}");
    }
}