using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.FuelPrices;

public class FuelPriceStatisticsDto
{
    [JsonPropertyName("average")]
    public double? Average { get; set; }

    [JsonPropertyName("station_count")]
    public int StationCount { get; set; }
}
