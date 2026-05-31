using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Economy;

[NamedCommand("richest", Aliases = ["moneytop", "moneyleaderboard", "topbucks"])]
public class MoneyLeaderboardCommand : Command
{
    private const int LEADERBOARD_SIZE = 20;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;

    public MoneyLeaderboardCommand(IBotDbContextFactory dbContextFactory, ITemplatesManager templatesManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "money_leaderboard_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.IsBucksEnabledAsync(cancellationToken))
        {
            context.ReplyLocalizedMessage("bucks_disabled");
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var richest = await dbContext.RoomUsers
            .Where(roomUser => roomUser.RoomId == context.RoomId && roomUser.Money > IMoneyService.DEFAULT_BALANCE)
            .OrderByDescending(roomUser => roomUser.Money)
            .Take(LEADERBOARD_SIZE)
            .Select(roomUser => new { roomUser.Id, roomUser.Money })
            .ToListAsync(cancellationToken);

        if (richest.Count == 0)
        {
            context.ReplyLocalizedMessage("money_leaderboard_empty");
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Economy/MoneyLeaderboard", new MoneyLeaderboardViewModel
        {
            Culture = context.Culture,
            Leaderboard = richest.Select(account => new KeyValuePair<string, long>(account.Id, account.Money)).ToList()
        });

        context.Reply($"/addhtmlbox {template.RemoveNewlines()}");
    }
}
