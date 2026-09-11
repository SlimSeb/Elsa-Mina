using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Logging;
using System.Text.Json;

namespace ElsaMina.Commands;

public class DataManager : IDataManager
{
    private const string DATA_DIRECTORY_NAME = "Data";

    private readonly string _dataDirectory;
    private readonly Lazy<ICountriesGameData> _countriesGameData;
    private readonly Lazy<IReadOnlyList<PokemonDescription>> _pokemonDescriptions;
    private readonly Lazy<ICapitalCitiesGameData> _capitalCitiesGameData;
    private readonly Lazy<IReadOnlyList<string>> _wordleWords;
    private readonly Lazy<IReadOnlyList<string>> _wordleWordsFr;
    private readonly Lazy<IReadOnlyList<string>> _semantixWordsFr;
    private readonly Lazy<IReadOnlyList<string>> _semantixAnswersFr;

    public DataManager() : this(DATA_DIRECTORY_NAME)
    {
    }

    public DataManager(string dataDirectory)
    {
        _dataDirectory = dataDirectory;

        _countriesGameData = new Lazy<ICountriesGameData>(LoadCountriesGameData);
        _pokemonDescriptions = new Lazy<IReadOnlyList<PokemonDescription>>(() =>
            LoadDataFromFile("pokedesc.json", ElsaMinaCommandsJsonContext.Default.ListPokemonDescription));
        _capitalCitiesGameData = new Lazy<ICapitalCitiesGameData>(LoadCapitalCitiesGameData);
        _wordleWords = new Lazy<IReadOnlyList<string>>(() =>
            LoadDataFromFile("wordle_words.json", ElsaMinaCommandsJsonContext.Default.ListString));
        _wordleWordsFr = new Lazy<IReadOnlyList<string>>(() =>
            LoadDataFromFile("wordle_words_fr.json", ElsaMinaCommandsJsonContext.Default.ListString));
        _semantixWordsFr = new Lazy<IReadOnlyList<string>>(() =>
            LoadDataFromFile("semantix_words_fr.json", ElsaMinaCommandsJsonContext.Default.ListString));
        _semantixAnswersFr = new Lazy<IReadOnlyList<string>>(() =>
            LoadDataFromFile("semantix_answers_fr.json", ElsaMinaCommandsJsonContext.Default.ListString));
    }

    public ICountriesGameData CountriesGameData => _countriesGameData.Value;
    public IReadOnlyList<PokemonDescription> PokemonDescriptions => _pokemonDescriptions.Value;
    public ICapitalCitiesGameData CapitalCitiesGameData => _capitalCitiesGameData.Value;
    public IReadOnlyList<string> WordleWords => _wordleWords.Value;
    public IReadOnlyList<string> WordleWordsFr => _wordleWordsFr.Value;
    public IReadOnlyList<string> SemantixWordsFr => _semantixWordsFr.Value;
    public IReadOnlyList<string> SemantixAnswersFr => _semantixAnswersFr.Value;

    private ICountriesGameData LoadCountriesGameData()
    {
        return LoadDataFromFile("countries_game.json", ElsaMinaCommandsJsonContext.Default.CountriesGameData)
            ?? new CountriesGameData { Countries = [] };
    }

    private ICapitalCitiesGameData LoadCapitalCitiesGameData()
    {
        var capitalsList = LoadDataFromFile("capital_cities.json", ElsaMinaCommandsJsonContext.Default.ListCapitalCityData);
        return new CapitalCitiesGameData { Capitals = capitalsList ?? [] };
    }

    private T LoadDataFromFile<T>(string fileName, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo)
    {
        var filePath = Path.Join(_dataDirectory, fileName);
        try
        {
            using var stream = File.OpenRead(filePath);
            var data = JsonSerializer.Deserialize(stream, jsonTypeInfo);
            Log.Information("Loaded data from {0}", fileName);
            return data;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load data from {0}", filePath);
            return default;
        }
    }
}
