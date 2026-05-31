using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Economy;

[NamedCommand("transfer", Aliases = ["pay", "send-money"])]
public class TransferMoneyCommand : Command
{
    private readonly IMoneyService _moneyService;

    public TransferMoneyCommand(IMoneyService moneyService)
    {
        _moneyService = moneyService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "transfer_money_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.IsBucksEnabledAsync(cancellationToken))
        {
            context.ReplyLocalizedMessage("bucks_disabled");
            return;
        }

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

        var senderBalance = await _moneyService.GetBalanceAsync(context.RoomId, senderId, cancellationToken);
        if (senderBalance < amount)
        {
            context.ReplyLocalizedMessage("transfer_money_insufficient_funds", senderBalance);
            return;
        }

        var newSenderBalance = await _moneyService.AddAsync(context.RoomId, senderId, -amount, cancellationToken);
        await _moneyService.AddAsync(context.RoomId, recipientId, amount, cancellationToken);

        context.ReplyLocalizedMessage("transfer_money_success", amount, recipientName, newSenderBalance);
    }
}
