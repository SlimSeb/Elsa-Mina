using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Economy;

[NamedCommand("givemoney", Aliases = ["give-money"])]
public class GiveMoneyCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public GiveMoneyCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Admin;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "give_money_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',');
        if (parts.Length != 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var recipientName = parts[0].Trim();
        var recipientId = recipientName.ToLowerAlphaNum();
        if (string.IsNullOrEmpty(recipientId))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        if (!long.TryParse(parts[1].Trim(), out var amount) || amount <= 0)
        {
            context.ReplyLocalizedMessage("money_invalid_amount");
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var account = await dbContext.Money.FindAsync([recipientId], cancellationToken);
        if (account == null)
        {
            account = new Money { Id = recipientId, Amount = amount };
            await dbContext.Money.AddAsync(account, cancellationToken);
        }
        else
        {
            account.Amount += amount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        context.ReplyLocalizedMessage("give_money_success", amount, recipientName, account.Amount);
    }
}
