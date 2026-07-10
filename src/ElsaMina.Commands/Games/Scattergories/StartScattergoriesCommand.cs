using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Scattergories;

[NamedCommand("scattergories", Aliases = ["scatt", "pokecategory", "category"])]
public class StartScattergoriesCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly IEventAnnouncer _eventAnnouncer;

    public StartScattergoriesCommand(IDependencyContainerService dependencyContainerService,
        IArcadeEventsService arcadeEventsService,
        IEventAnnouncer eventAnnouncer)
    {
        _dependencyContainerService = dependencyContainerService;
        _arcadeEventsService = arcadeEventsService;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IScattergoriesGame)
        {
            context.ReplyLocalizedMessage("scattergories_already_running");
            return;
        }

        if (context.Room?.Game is not null)
        {
            context.ReplyLocalizedMessage("scattergories_other_game_running");
            return;
        }

        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        var game = _dependencyContainerService.Resolve<ScattergoriesGame>();
        game.Context = context;
        context.Room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game,
            "scattergories_started_in", [context.RoomId], cancellationToken);

        await game.StartAsync();
    }
}
