using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarot", Aliases = ["frenchtarot"])]
public class StartTarotCommand : GameCommand
{
    private readonly Func<TarotGame> _gameFactory;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly IEventAnnouncer _eventAnnouncer;

    public StartTarotCommand(Func<TarotGame> gameFactory,
        IArcadeEventsService arcadeEventsService,
        IEventAnnouncer eventAnnouncer)
    {
        _gameFactory = gameFactory;
        _arcadeEventsService = arcadeEventsService;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "tarot_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room is null)
        {
            return;
        }

        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        if (context.Room.Game is ITarotGame)
        {
            context.ReplyLocalizedMessage("tarot_already_running");
            return;
        }

        if (context.Room.Game is not null)
        {
            context.ReplyLocalizedMessage("tarot_other_game_running");
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        context.Room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game, "tarot_started_in",
            [context.RoomId], cancellationToken);

        context.ReplyLocalizedMessage("tarot_game_created", context.Sender.Name);
        await game.BeginJoinPhaseAsync();
    }
}
