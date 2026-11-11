namespace ElsaMina.Commands.Misc.FuelPrices;

/// <summary>
/// The stations prices are computed over: the whole country, a department code, a postal code or a city name
/// as written in the dataset.
/// </summary>
public sealed record FuelPriceArea(FuelPriceAreaKind Kind, string Value)
{
    public static FuelPriceArea Country { get; } = new(FuelPriceAreaKind.Country, null);
}
