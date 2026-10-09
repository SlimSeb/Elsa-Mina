using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.EventAnnounces;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

[NamedCommand("chess", Aliases = ["echecs"])]
public class CreateChessCommand : GameCommand
{
    private readonly Func<ChessGame> _gameFactory;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly IEventAnnouncer _eventAnnouncer;

    public CreateChessCommand(Func<ChessGame> gameFactory,
        IArcadeEventsService arcadeEventsService,
        IEventAnnouncer eventAnnouncer)
    {
        _gameFactory = gameFactory;
        _arcadeEventsService = arcadeEventsService;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        if (room is null)
        {
            return;
        }

        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        if (room.Game is not null)
        {
            context.ReplyLocalizedMessage("chess_game_start_already_exist");
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game, "chess_started_in",
            [context.RoomId], cancellationToken);

        await game.DisplayAnnounce();
    }
}
