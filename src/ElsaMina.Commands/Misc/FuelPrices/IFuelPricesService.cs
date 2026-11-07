namespace ElsaMina.Commands.Misc.FuelPrices;

public interface IFuelPricesService
{
    /// <summary>
    /// Returns the names, as written in the dataset, of the cities with a station whose name contains every word of
    /// <paramref name="name"/> (case and accents ignored).
    /// </summary>
    Task<IReadOnlyList<string>> FindCitiesAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the average and cheapest price of each fuel in the area, over the prices updated recently. A fuel
    /// with no recent price in the area is left out.
    /// </summary>
    Task<IReadOnlyList<FuelPriceSummary>> GetSummariesAsync(FuelPriceArea area,
        CancellationToken cancellationToken = default);
}
