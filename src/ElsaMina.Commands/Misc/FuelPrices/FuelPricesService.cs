using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Misc.FuelPrices;

/// <summary>
/// Reads the live fuel prices of French stations published by the Ministry of the Economy
/// (https://data.economie.gouv.fr, an Opendatasoft API queried in ODSQL).
/// </summary>
public class FuelPricesService : IFuelPricesService
{
    /// <summary>
    /// Stations that stopped reporting keep their last price in the feed, sometimes for years. Only prices updated
    /// within this many days are counted, so a stale price is never shown as the cheapest one.
    /// </summary>
    public const int RECENT_PRICE_DAYS = 7;

    private const string RECORDS_URL =
        "https://data.economie.gouv.fr/api/explore/v2.1/catalog/datasets/prix-des-carburants-en-france-flux-instantane-v2/records";

    private const int MAX_CITY_RESULTS = 20;

    private readonly IHttpService _httpService;

    public FuelPricesService(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<IReadOnlyList<string>> FindCitiesAsync(string name,
        CancellationToken cancellationToken = default)
    {
        var request = HttpRequest.Get(RECORDS_URL)
            .WithQueryParameter("where", $"ville like {ToStringLiteral(name)}")
            .WithQueryParameter("group_by", "ville as city")
            .WithQueryParameter("limit", MAX_CITY_RESULTS.ToString());
        var response = await _httpService.SendAsync<OpenDataRecordsResponse<FuelCityDto>>(request, cancellationToken);
        return response.Data?.Results?
            .Select(result => result.City)
            .Where(city => !string.IsNullOrEmpty(city))
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<FuelPriceSummary>> GetSummariesAsync(FuelPriceArea area,
        CancellationToken cancellationToken = default)
    {
        var summaries = await Task.WhenAll(FuelType.All.Select(fuel =>
            GetSummaryAsync(fuel, area, cancellationToken)));
        return summaries.Where(summary => summary != null).ToList();
    }

    private async Task<FuelPriceSummary> GetSummaryAsync(FuelType fuel, FuelPriceArea area,
        CancellationToken cancellationToken)
    {
        var filter = BuildFilter(fuel, area);
        var statisticsRequest = HttpRequest.Get(RECORDS_URL)
            .WithQueryParameter("select",
                $"avg({fuel.PriceField}) as average, count({fuel.PriceField}) as station_count")
            .WithQueryParameter("where", filter)
            .WithQueryParameter("limit", "1");
        var cheapestStationRequest = HttpRequest.Get(RECORDS_URL)
            .WithQueryParameter("select",
                $"adresse as address, ville as city, cp as postal_code, {fuel.PriceField} as price, {fuel.UpdateDateField} as updated_at")
            .WithQueryParameter("where", filter)
            .WithQueryParameter("order_by", $"{fuel.PriceField} asc")
            .WithQueryParameter("limit", "1");

        var statisticsTask = _httpService.SendAsync<OpenDataRecordsResponse<FuelPriceStatisticsDto>>(
            statisticsRequest, cancellationToken);
        var cheapestStationTask = _httpService.SendAsync<OpenDataRecordsResponse<FuelStationDto>>(
            cheapestStationRequest, cancellationToken);
        var statistics = (await statisticsTask).Data?.Results?.FirstOrDefault();
        var cheapestStation = (await cheapestStationTask).Data?.Results?.FirstOrDefault();

        if (statistics?.Average == null || statistics.StationCount == 0 || cheapestStation == null)
        {
            return null;
        }

        return new FuelPriceSummary(fuel, statistics.Average.Value, statistics.StationCount, cheapestStation);
    }

    private static string BuildFilter(FuelType fuel, FuelPriceArea area)
    {
        var recentPriceFilter =
            $"{fuel.PriceField} is not null and {fuel.UpdateDateField} >= now(days=-{RECENT_PRICE_DAYS})";
        var areaFilter = area.Kind switch
        {
            FuelPriceAreaKind.Department => $"code_departement = {ToStringLiteral(area.Value)}",
            FuelPriceAreaKind.PostalCode => $"cp = {ToStringLiteral(area.Value)}",
            FuelPriceAreaKind.City => $"ville = {ToStringLiteral(area.Value)}",
            _ => null
        };
        return areaFilter == null ? recentPriceFilter : $"{areaFilter} and {recentPriceFilter}";
    }

    private static string ToStringLiteral(string value)
    {
        return $"\"{value.Replace(@"\", @"\\").Replace("\"", "\\\"")}\"";
    }
}
