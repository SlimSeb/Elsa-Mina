using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.FuelPrices;

public class FuelCityDto
{
    [JsonPropertyName("city")]
    public string City { get; set; }
}
