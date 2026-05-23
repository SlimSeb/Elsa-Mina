using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Core;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Commands;

public class DataManager : IDataManager
{
    private const string DATA_DIRECTORY_NAME = "Data";

    public ICountriesGameData CountriesGameData { get; private set; }
    public IReadOnlyList<PokemonDescription> PokemonDescriptions { get; private set; }
    public ICapitalCitiesGameData CapitalCitiesGameData { get; private set; }

    public async Task Initialize()
    {
        CountriesGameData =
            await GetDataFromFile<CountriesGameData>(Path.Join(DATA_DIRECTORY_NAME, "countries_game.json")).ConfigureAwait(false);
        PokemonDescriptions =
            await GetDataFromFile<List<PokemonDescription>>(Path.Join(DATA_DIRECTORY_NAME, "pokedesc.json")).ConfigureAwait(false);
        var capitalsList =
            await GetDataFromFile<List<CapitalCityData>>(Path.Join(DATA_DIRECTORY_NAME, "capital_cities.json")).ConfigureAwait(false);
        CapitalCitiesGameData = new CapitalCitiesGameData { Capitals = capitalsList };

        Log.Information("Fetched countries, capital cities & pokemon descriptions.");
    }

    private static async Task<T> GetDataFromFile<T>(string filePath)
    {
        using var streamReader = new StreamReader(filePath);
        var fileContent = await streamReader.ReadToEndAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(fileContent);
    }
}