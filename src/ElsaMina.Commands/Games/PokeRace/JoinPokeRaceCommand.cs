using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.PokeRace;

[NamedCommand("racejoin", Aliases = ["joinrace"])]
public class JoinPokeRaceCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPokeRaceGame pokeRace)
        {
            context.ReplyLocalizedMessage("pokerace_not_running");
            return Task.CompletedTask;
        }

        var target = context.Target.Trim();
        if (string.IsNullOrEmpty(target))
        {
            var available = string.Join(", ", PokeRaceConstants.RACE_POKEMON.Keys
                .Where(pokemon => pokeRace.Players.Values.All(player => player.Pokemon != pokemon)));
            context.Reply(available.Length > 0
                ? $"Choisissez un Pokémon parmi: {available}"
                : "Tous les Pokémon ont déjà été choisis!");
            return Task.CompletedTask;
        }

        var pokemonName = PokeRaceConstants.RACE_POKEMON.Keys
            .FirstOrDefault(pokemon => string.Equals(pokemon, target, StringComparison.OrdinalIgnoreCase));

        if (pokemonName is null)
        {
            context.ReplyLocalizedMessage("pokerace_join_invalid_pokemon",
                target,
                string.Join(", ", PokeRaceConstants.RACE_POKEMON.Keys));
            return Task.CompletedTask;
        }

        var (_, messageKey, args) = pokeRace.JoinRace(context.Sender.Name, pokemonName);
        context.ReplyLocalizedMessage(messageKey, args);

        return Task.CompletedTask;
    }
}