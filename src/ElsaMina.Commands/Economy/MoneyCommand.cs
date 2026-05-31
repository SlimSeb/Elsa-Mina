using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Economy;

[NamedCommand("money", Aliases = ["balance", "wallet"])]
public class MoneyCommand : Command
{
    private readonly IMoneyService _moneyService;

    public MoneyCommand(IMoneyService moneyService)
    {
        _moneyService = moneyService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "money_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.IsBucksEnabledAsync(cancellationToken))
        {
            context.ReplyLocalizedMessage("bucks_disabled");
            return;
        }

        var targetName = string.IsNullOrWhiteSpace(context.Target) ? context.Sender.Name : context.Target.Trim();
        var targetId = targetName.ToLowerAlphaNum();
        if (string.IsNullOrEmpty(targetId))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var amount = await _moneyService.GetBalanceAsync(context.RoomId, targetId, cancellationToken);

        context.ReplyLocalizedMessage("money_balance", targetName, amount);
    }
}
