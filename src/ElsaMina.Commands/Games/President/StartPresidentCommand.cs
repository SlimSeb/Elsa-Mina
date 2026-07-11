using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("president", Aliases = ["trouducul", "presidentgame"])]
public class StartPresidentCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly IEventAnnouncer _eventAnnouncer;

    public StartPresidentCommand(IDependencyContainerService dependencyContainerService,
        IArcadeEventsService arcadeEventsService,
        IEventAnnouncer eventAnnouncer)
    {
        _dependencyContainerService = dependencyContainerService;
        _arcadeEventsService = arcadeEventsService;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "president_help";

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

        if (context.Room.Game is IPresidentGame)
        {
            context.ReplyLocalizedMessage("president_already_running");
            return;
        }

        if (context.Room.Game is not null)
        {
            context.ReplyLocalizedMessage("president_other_game_running");
            return;
        }

        var rounds = PresidentConstants.DEFAULT_ROUNDS;
        var argument = context.Target?.Trim();
        if (!string.IsNullOrEmpty(argument))
        {
            if (!int.TryParse(argument, out rounds) || rounds < 1 || rounds > PresidentConstants.MAX_ROUNDS)
            {
                context.ReplyLocalizedMessage("president_rounds_invalid", PresidentConstants.MAX_ROUNDS);
                return;
            }
        }

        var game = _dependencyContainerService.Resolve<PresidentGame>();
        game.Context = context;
        game.TotalRounds = rounds;
        context.Room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game,
            "president_started_in", [context.RoomId], cancellationToken);

        context.ReplyLocalizedMessage("president_game_created", context.Sender.Name);
        await game.BeginJoinPhaseAsync();
    }
}
