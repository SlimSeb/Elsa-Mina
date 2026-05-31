using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Economy;

[NamedCommand("givemoney", Aliases = ["give-money"])]
public class GiveMoneyCommand : Command
{
    private readonly IMoneyService _moneyService;

    public GiveMoneyCommand(IMoneyService moneyService)
    {
        _moneyService = moneyService;
    }

    public override Rank RequiredRank => Rank.Admin;
    public override string HelpMessageKey => "give_money_help";

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

        if (!long.TryParse(parts[1].Trim(), out var amount) || amount <= 0)
        {
            context.ReplyLocalizedMessage("money_invalid_amount");
            return;
        }

        var newBalance = await _moneyService.AddAsync(context.RoomId, recipientId, amount, cancellationToken);

        context.ReplyLocalizedMessage("give_money_success", amount, recipientName, newBalance);
    }
}
