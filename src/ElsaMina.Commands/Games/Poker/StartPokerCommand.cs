using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("poker", Aliases = ["texasholdem", "holdem"])]
public class StartPokerCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;

    public StartPokerCommand(IDependencyContainerService dependencyContainerService)
    {
        _dependencyContainerService = dependencyContainerService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "poker_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room is null)
        {
            return;
        }

        if (context.Room.Game is IPokerGame)
        {
            context.ReplyLocalizedMessage("poker_already_running");
            return;
        }

        if (context.Room.Game is not null)
        {
            context.ReplyLocalizedMessage("poker_other_game_running");
            return;
        }

        var buyIn = PokerConstants.DEFAULT_BUY_IN;
        if (!string.IsNullOrWhiteSpace(context.Target))
        {
            if (!long.TryParse(context.Target.Trim(), out buyIn) || buyIn < PokerConstants.MIN_BUY_IN)
            {
                context.ReplyLocalizedMessage("poker_invalid_buy_in", PokerConstants.MIN_BUY_IN);
                return;
            }
        }

        var isForFun = !await context.IsBucksEnabledAsync(cancellationToken);

        var game = _dependencyContainerService.Resolve<PokerGame>();
        game.Context = context;
        game.BuyIn = buyIn;
        game.IsForFun = isForFun;
        context.Room.Game = game;

        context.ReplyLocalizedMessage("poker_game_created", context.Sender.Name, buyIn);
        if (isForFun)
        {
            context.ReplyLocalizedMessage("poker_for_fun_mode");
        }

        await game.BeginJoinPhaseAsync();
    }
}
