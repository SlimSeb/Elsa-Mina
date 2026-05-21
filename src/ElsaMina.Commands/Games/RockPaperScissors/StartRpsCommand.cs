using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("rps", Aliases = ["rockpaperscissors"])]
public class StartRpsCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;

    public StartRpsCommand(IDependencyContainerService dependencyContainerService)
    {
        _dependencyContainerService = dependencyContainerService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "rps_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IRpsGame)
        {
            context.ReplyLocalizedMessage("rps_already_running");
            return Task.CompletedTask;
        }

        if (context.Room?.Game is not null)
        {
            context.ReplyLocalizedMessage("rps_other_game_running");
            return Task.CompletedTask;
        }

        var game = _dependencyContainerService.Resolve<RpsGame>();
        game.Context = context;
        context.Room!.Game = game;

        context.ReplyLocalizedMessage("rps_game_created");
        return Task.CompletedTask;
    }
}
