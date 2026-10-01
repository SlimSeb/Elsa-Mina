using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("poker", Aliases = ["texasholdem", "holdem"])]
public class StartPokerCommand : GameCommand
{
    private readonly Func<PokerGame> _gameFactory;
    private readonly IEventAnnouncer _eventAnnouncer;

    public StartPokerCommand(Func<PokerGame> gameFactory,
        IEventAnnouncer eventAnnouncer)
    {
        _gameFactory = gameFactory;
        _eventAnnouncer = eventAnnouncer;
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
        if (!string.IsNullOrWhiteSpace(context.Target)
            && (!long.TryParse(context.Target.Trim(), out buyIn) || buyIn < PokerConstants.MIN_BUY_IN))
        {
            context.ReplyLocalizedMessage("poker_invalid_buy_in", PokerConstants.MIN_BUY_IN);
            return;
        }

        var isForFun = !await context.IsBucksEnabledAsync(cancellationToken);

        var game = _gameFactory();
        game.Context = context;
        game.BuyIn = buyIn;
        game.IsForFun = isForFun;
        context.Room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game, "poker_started_in",
            [context.RoomId], cancellationToken);

        context.ReplyLocalizedMessage("poker_game_created", context.Sender.Name, buyIn);
        if (isForFun)
        {
            context.ReplyLocalizedMessage("poker_for_fun_mode");
        }

        await game.BeginJoinPhaseAsync();
    }
}
