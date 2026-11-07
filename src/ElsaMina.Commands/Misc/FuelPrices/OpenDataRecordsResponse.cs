using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.FuelPrices;

public class OpenDataRecordsResponse<TRecord>
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("results")]
    public List<TRecord> Results { get; set; }
}
