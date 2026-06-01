using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Wordle;

[NamedCommand("wlleaderboard", Aliases = ["wll"])]
public class WordleLeaderboardCommand : Command
{
    private const int MAX_COUNT = 20;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public WordleLeaderboardCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var leaderboard = await dbContext.WordleScores
            .OrderByDescending(entry => entry.MaxStreak)
            .ThenByDescending(entry => entry.Wins)
            .ThenByDescending(entry => entry.CurrentStreak)
            .Take(MAX_COUNT)
            .ToListAsync(cancellationToken);

        if (leaderboard.Count == 0)
        {
            context.ReplyRankAwareLocalizedMessage("wordle_leaderboard_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Wordle/WordleLeaderboard",
            new WordleLeaderboardViewModel
            {
                Culture = context.Culture,
                Leaderboard = leaderboard
            });

        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}
