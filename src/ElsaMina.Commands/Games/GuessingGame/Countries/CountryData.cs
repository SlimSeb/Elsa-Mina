using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Games.GuessingGame.Countries;

public class CountryData
{
    [JsonPropertyName("english_name")]
    public string EnglishName { get; set; }
    [JsonPropertyName("french_name")]
    public string FrenchName { get; set; }
    [JsonPropertyName("flag")]
    public string Flag { get; set; }
    [JsonPropertyName("location")]
    public string Location { get; set; }
}