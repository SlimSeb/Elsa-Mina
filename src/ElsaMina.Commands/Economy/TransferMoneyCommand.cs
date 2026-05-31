using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Economy;

[NamedCommand("transfer", Aliases = ["pay", "send-money"])]
public class TransferMoneyCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public TransferMoneyCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "transfer_money_help";

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

        var senderId = context.Sender.UserId;
        if (recipientId == senderId)
        {
            context.ReplyLocalizedMessage("transfer_money_self");
            return;
        }

        if (!long.TryParse(parts[1].Trim(), out var amount) || amount <= 0)
        {
            context.ReplyLocalizedMessage("money_invalid_amount");
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var senderAccount = await dbContext.Money.FindAsync([senderId, context.RoomId], cancellationToken);
        if (senderAccount == null || senderAccount.Amount < amount)
        {
            context.ReplyLocalizedMessage("transfer_money_insufficient_funds", senderAccount?.Amount ?? 0);
            return;
        }

        var recipientAccount = await dbContext.Money.FindAsync([recipientId, context.RoomId], cancellationToken);
        if (recipientAccount == null)
        {
            recipientAccount = new Money { Id = recipientId, RoomId = context.RoomId, Amount = 0 };
            await dbContext.Money.AddAsync(recipientAccount, cancellationToken);
        }

        senderAccount.Amount -= amount;
        recipientAccount.Amount += amount;

        await dbContext.SaveChangesAsync(cancellationToken);

        context.ReplyLocalizedMessage("transfer_money_success", amount, recipientName, senderAccount.Amount);
    }
}
