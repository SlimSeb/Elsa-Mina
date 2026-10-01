using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.PokeRace;

[NamedCommand("racestart", Aliases = ["startrace"])]
public class StartRaceCommand : GameCommand
{
    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPokeRaceGame pokeRace)
        {
            context.ReplyLocalizedMessage("pokerace_not_running");
            return;
        }

        if (pokeRace.IsStarted)
        {
            context.ReplyLocalizedMessage("pokerace_race_already_started");
            return;
        }

        if (pokeRace.Players.Count < PokeRaceConstants.MIN_PLAYERS)
        {
            context.ReplyLocalizedMessage("pokerace_min_players_required", PokeRaceConstants.MIN_PLAYERS);
            return;
        }

        await pokeRace.StartRaceAsync();
    }
}