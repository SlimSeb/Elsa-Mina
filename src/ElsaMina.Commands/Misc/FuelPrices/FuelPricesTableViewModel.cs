using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.FuelPrices;

public class FuelPricesTableViewModel : LocalizableViewModel
{
    public string AreaName { get; init; }
    public IReadOnlyList<FuelPriceSummary> Summaries { get; init; }
    public int RecentPriceDays { get; init; }
}
