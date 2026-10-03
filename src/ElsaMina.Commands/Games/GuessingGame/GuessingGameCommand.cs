using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.EventAnnounces;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.GuessingGame;

[NamedCommand("guessinggame", Aliases = ["countriesgame", "pokedesc", "pokecries", "gatekeepers", "capitalcities", "higherlower", "whosthatpokemon", "trivia"])]
public class GuessingGameCommand : GameCommand
{
    private const int MAX_TURNS_COUNT = 20;

    private readonly IGuessingGameFactory _guessingGameFactory;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly IEventAnnouncer _eventAnnouncer;

    public GuessingGameCommand(IGuessingGameFactory guessingGameFactory,
        IArcadeEventsService arcadeEventsService,
        IEventAnnouncer eventAnnouncer)
    {
        _guessingGameFactory = guessingGameFactory;
        _arcadeEventsService = arcadeEventsService;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        if (!int.TryParse(context.Target, out var turnsCount))
        {
            context.ReplyLocalizedMessage("guessing_game_specify");
            return;
        }

        if (turnsCount is <= 0 or > MAX_TURNS_COUNT)
        {
            context.ReplyLocalizedMessage("guessing_game_invalid_number_turns", MAX_TURNS_COUNT);
            return;
        }

        var room = context.Room;
        if (room.Game != null)
        {
            context.ReplyLocalizedMessage("guessing_game_currently_ongoing");
            return;
        }

        var game = _guessingGameFactory.Create(context.Command);
        if (game == null)
        {
            context.ReplyLocalizedMessage("guessing_game_invalid_command");
            return;
        }

        game.TurnsCount = turnsCount;
        game.Context = context;

        room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game,
            "guessing_game_started_in", [context.RoomId], cancellationToken);

        await game.Start();
    }
}