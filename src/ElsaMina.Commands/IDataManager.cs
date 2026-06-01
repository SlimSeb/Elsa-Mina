using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;

namespace ElsaMina.Commands;

public interface IDataManager
{
    ICountriesGameData CountriesGameData { get; }
    IReadOnlyList<PokemonDescription> PokemonDescriptions { get; }
    ICapitalCitiesGameData CapitalCitiesGameData { get; }
    IReadOnlyList<string> WordleWords { get; }
    IReadOnlyList<string> WordleWordsFr { get; }
}