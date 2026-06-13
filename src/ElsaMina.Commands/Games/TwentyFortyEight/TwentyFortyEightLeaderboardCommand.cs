using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.TwentyFortyEight;

[NamedCommand("2048leaderboard", Aliases = ["2048lb"])]
public class TwentyFortyEightLeaderboardCommand : Command
{
    private const int MAX_COUNT = 20;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public TwentyFortyEightLeaderboardCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var leaderboard = await dbContext.TwentyFortyEightScores
            .Include(entry => entry.User)
            .OrderByDescending(entry => entry.BestScore)
            .ThenByDescending(entry => entry.Wins)
            .Take(MAX_COUNT)
            .ToListAsync(cancellationToken);

        if (leaderboard.Count == 0)
        {
            context.ReplyRankAwareLocalizedMessage("tfe_leaderboard_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/TwentyFortyEight/TwentyFortyEightLeaderboard",
            new TwentyFortyEightLeaderboardViewModel
            {
                Culture = context.Culture,
                Leaderboard = leaderboard
            });

        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}
