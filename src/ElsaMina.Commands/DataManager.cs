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
    public IReadOnlyList<string> WordleWords { get; private set; }
    public IReadOnlyList<string> WordleWordsFr { get; private set; }

    public async Task Initialize()
    {
        CountriesGameData =
            await GetDataFromFile<CountriesGameData>(Path.Join(DATA_DIRECTORY_NAME, "countries_game.json"));
        PokemonDescriptions =
            await GetDataFromFile<List<PokemonDescription>>(Path.Join(DATA_DIRECTORY_NAME, "pokedesc.json"));
        var capitalsList =
            await GetDataFromFile<List<CapitalCityData>>(Path.Join(DATA_DIRECTORY_NAME, "capital_cities.json"));
        CapitalCitiesGameData = new CapitalCitiesGameData { Capitals = capitalsList };
        WordleWords =
            await GetDataFromFile<List<string>>(Path.Join(DATA_DIRECTORY_NAME, "wordle_words.json"));
        WordleWordsFr =
            await GetDataFromFile<List<string>>(Path.Join(DATA_DIRECTORY_NAME, "wordle_words_fr.json"));

        Log.Information("Fetched countries, capital cities, pokemon descriptions & wordle words.");
    }

    private static async Task<T> GetDataFromFile<T>(string filePath)
    {
        using var streamReader = new StreamReader(filePath);
        var fileContent = await streamReader.ReadToEndAsync();
        return JsonConvert.DeserializeObject<T>(fileContent);
    }
}