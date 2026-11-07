namespace ElsaMina.Commands.Misc.FuelPrices;

public sealed record FuelPriceSummary(
    FuelType Fuel,
    double AveragePrice,
    int StationCount,
    FuelStationDto CheapestStation);
