using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarot", Aliases = ["frenchtarot"])]
public class StartTarotCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IArcadeEventsService _arcadeEventsService;

    public StartTarotCommand(IDependencyContainerService dependencyContainerService,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
        _arcadeEventsService = arcadeEventsService;
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

        var game = _dependencyContainerService.Resolve<TarotGame>();
        game.Context = context;
        context.Room.Game = game;

        context.ReplyLocalizedMessage("tarot_game_created", context.Sender.Name);
        await game.BeginJoinPhaseAsync();
    }
}
