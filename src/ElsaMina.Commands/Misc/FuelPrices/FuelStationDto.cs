using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.FuelPrices;

/// <summary>
/// A station and its price for one fuel, read with the aliases <see cref="FuelPricesService"/> selects.
/// </summary>
public class FuelStationDto
{
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
