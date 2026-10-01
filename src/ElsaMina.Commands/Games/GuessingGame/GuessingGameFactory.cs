using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.Gatekeepers;
using ElsaMina.Commands.Games.GuessingGame.HigherLower;
using ElsaMina.Commands.Games.GuessingGame.PokeCries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;

namespace ElsaMina.Commands.Games.GuessingGame;

public class GuessingGameFactory : IGuessingGameFactory
{
    private readonly Dictionary<string, Func<GuessingGame>> _factoriesByCommand;

    public GuessingGameFactory(
        Func<CountriesGame> countriesGameFactory,
        Func<PokeDescGame> pokeDescGameFactory,
        Func<PokeCriesGame> pokeCriesGameFactory,
        Func<GatekeepersGame> gatekeepersGameFactory,
        Func<CapitalCitiesGame> capitalCitiesGameFactory,
        Func<HigherLowerGame> higherLowerGameFactory,
        Func<WhosThatPokemonGame> whosThatPokemonGameFactory,
        Func<TriviaGame> triviaGameFactory)
    {
        _factoriesByCommand = new Dictionary<string, Func<GuessingGame>>
        {
            ["countriesgame"] = countriesGameFactory,
            ["pokedesc"] = pokeDescGameFactory,
            ["pokecries"] = pokeCriesGameFactory,
            ["gatekeepers"] = gatekeepersGameFactory,
            ["capitalcities"] = capitalCitiesGameFactory,
            ["higherlower"] = higherLowerGameFactory,
            ["whosthatpokemon"] = whosThatPokemonGameFactory,
            ["trivia"] = triviaGameFactory
        };
    }

    public GuessingGame Create(string commandName)
    {
        return commandName != null && _factoriesByCommand.TryGetValue(commandName, out var factory)
            ? factory()
            : null;
    }
}
