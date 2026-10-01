using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.PokeRace;

[NamedCommand("raceend", Aliases = ["endrace"])]
public class EndRaceCommand : GameCommand
{
    public override Rank RequiredRank => Rank.Driver;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPokeRaceGame pokeRace)
        {
            context.ReplyLocalizedMessage("pokerace_not_running");
            return Task.CompletedTask;
        }

        pokeRace.Cancel();
        context.ReplyLocalizedMessage("pokerace_cancelled");
        return Task.CompletedTask;
    }
}
