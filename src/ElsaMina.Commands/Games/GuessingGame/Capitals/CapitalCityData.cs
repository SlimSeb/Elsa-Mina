using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Games.GuessingGame.Capitals;

public class CapitalCityData
{
    [JsonPropertyName("country_en")]
    public string CountryEnglish { get; set; }
    [JsonPropertyName("capital_en")]
    public string CapitalEnglish { get; set; }
    [JsonPropertyName("country_fr")]
    public string CountryFrench { get; set; }
    [JsonPropertyName("capital_fr")]
    public string CapitalFrench { get; set; }
}