using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Games.GuessingGame.Countries;

public class CountriesGameData : ICountriesGameData
{
    [JsonPropertyName("values")]
    public IEnumerable<CountryData> Countries { get; set; }
}