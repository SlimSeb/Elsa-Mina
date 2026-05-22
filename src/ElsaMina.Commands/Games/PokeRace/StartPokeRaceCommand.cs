using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.PokeRace;

[NamedCommand("pokerace", Aliases = ["coursepokerace"])]
public class StartPokeRaceCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;

    public StartPokeRaceCommand(IDependencyContainerService dependencyContainerService)
    {
        _dependencyContainerService = dependencyContainerService;
    }

    public override Rank RequiredRank => Rank.Driver;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room.Game is IPokeRaceGame)
        {
            context.ReplyLocalizedMessage("pokerace_already_running");
            return;
        }

        if (context.Room.Game is not null)
        {
            context.ReplyLocalizedMessage("pokerace_other_game_running");
            return;
        }

        var game = _dependencyContainerService.Resolve<PokeRaceGame>();
        game.Context = context;
        context.Room.Game = game;
        await game.BeginJoinPhaseAsync();
    }
}