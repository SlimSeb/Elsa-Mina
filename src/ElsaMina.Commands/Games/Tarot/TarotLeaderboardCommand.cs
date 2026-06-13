using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotleaderboard", Aliases = ["tarotlb"])]
public class TarotLeaderboardCommand : Command
{
    private const int MAX_COUNT = 20;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public TarotLeaderboardCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var leaderboard = await dbContext.TarotStats
            .Include(entry => entry.User)
            .OrderByDescending(entry => entry.TotalScoreHalfPoints)
            .Take(MAX_COUNT)
            .ToListAsync(cancellationToken);

        if (leaderboard.Count == 0)
        {
            context.ReplyRankAwareLocalizedMessage("tarot_leaderboard_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Tarot/TarotLeaderboard",
            new TarotLeaderboardViewModel
            {
                Culture = context.Culture,
                Leaderboard = leaderboard
            });

        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}
