using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

[NamedCommand("chess", Aliases = ["echecs"])]
public class CreateChessCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IArcadeEventsService _arcadeEventsService;

    public CreateChessCommand(IDependencyContainerService dependencyContainerService,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
        _arcadeEventsService = arcadeEventsService;
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

        var game = _dependencyContainerService.Resolve<ChessGame>();
        game.Context = context;
        room.Game = game;
        await game.DisplayAnnounce();
    }
}
