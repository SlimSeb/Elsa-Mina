using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;

namespace ElsaMina.Commands.Economy;

[NamedCommand("money", Aliases = ["balance", "wallet"])]
public class MoneyCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public MoneyCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "money_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var targetName = string.IsNullOrWhiteSpace(context.Target) ? context.Sender.Name : context.Target.Trim();
        var targetId = targetName.ToLowerAlphaNum();
        if (string.IsNullOrEmpty(targetId))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var account = await dbContext.Money.FindAsync([targetId], cancellationToken);
        var amount = account?.Amount ?? 0;

        context.ReplyLocalizedMessage("money_balance", targetName, amount);
    }
}
