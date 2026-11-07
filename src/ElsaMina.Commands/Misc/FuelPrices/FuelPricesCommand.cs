using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.FuelPrices;

/// <summary>
/// Shows the average and cheapest price of each fuel in France, a department (<c>69</c>, <c>2A</c>), a postal code
/// (<c>75015</c>) or a city (<c>Lyon</c>).
/// </summary>
[NamedCommand("fuel", Aliases = ["carburant", "carburants", "essence", "gasprice", "gasprices"])]
public partial class FuelPricesCommand : Command
{
    private const string TEMPLATE_KEY = "Misc/FuelPrices/FuelPricesTable";

    private static readonly Regex POSTAL_CODE_REGEX = PostalCodeRegex();
    private static readonly Regex DEPARTMENT_CODE_REGEX = DepartmentCodeRegex();
    private static readonly Regex CITY_NAME_SEPARATORS_REGEX = CityNameSeparatorsRegex();

    private readonly IFuelPricesService _fuelPricesService;
    private readonly ITemplatesManager _templatesManager;

    public FuelPricesCommand(IFuelPricesService fuelPricesService, ITemplatesManager templatesManager)
    {
        _fuelPricesService = fuelPricesService;
        _templatesManager = templatesManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "fuelprices_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target?.Trim() ?? string.Empty;
        try
        {
            var area = await ResolveAreaAsync(context, target, cancellationToken);
            if (area == null)
            {
                return;
            }

            var summaries = await _fuelPricesService.GetSummariesAsync(area, cancellationToken);
            if (summaries.Count == 0)
            {
                context.ReplyRankAwareLocalizedMessage("fuelprices_no_prices", target);
                return;
            }

            var html = await _templatesManager.GetTemplateAsync(TEMPLATE_KEY, new FuelPricesTableViewModel
            {
                Culture = context.Culture,
                AreaName = GetAreaName(context, area),
                Summaries = summaries,
                RecentPriceDays = FuelPricesService.RECENT_PRICE_DAYS
            });
            context.ReplyHtml(html.RemoveNewlines(), rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not fetch fuel prices for '{Target}'.", target);
            context.ReplyRankAwareLocalizedMessage("fuelprices_error");
        }
    }

    private async Task<FuelPriceArea> ResolveAreaAsync(IContext context, string target,
        CancellationToken cancellationToken)
    {
        if (target.Length == 0)
        {
            return FuelPriceArea.Country;
        }

        if (POSTAL_CODE_REGEX.IsMatch(target))
        {
            return new FuelPriceArea(FuelPriceAreaKind.PostalCode, target);
        }

        if (DEPARTMENT_CODE_REGEX.IsMatch(target))
        {
            return new FuelPriceArea(FuelPriceAreaKind.Department, target.ToUpperInvariant().PadLeft(2, '0'));
        }

        var searchedName = NormalizeCityName(target);
        if (searchedName.Length == 0)
        {
            context.ReplyRankAwareLocalizedMessage("fuelprices_city_not_found", target);
            return null;
        }

        var cities = await _fuelPricesService.FindCitiesAsync(searchedName, cancellationToken);
        var city = cities.FirstOrDefault(name => NormalizeCityName(name) == searchedName)
                   ?? (cities.Count == 1 ? cities[0] : null);
        if (city != null)
        {
            return new FuelPriceArea(FuelPriceAreaKind.City, city);
        }

        if (cities.Count == 0)
        {
            context.ReplyRankAwareLocalizedMessage("fuelprices_city_not_found", target);
        }
        else
        {
            context.ReplyRankAwareLocalizedMessage("fuelprices_ambiguous_city", target, string.Join(", ", cities));
        }

        return null;
    }

    private static string GetAreaName(IContext context, FuelPriceArea area)
    {
        return area.Kind switch
        {
            FuelPriceAreaKind.Country => context.GetString("fuelprices_area_country"),
            FuelPriceAreaKind.Department => context.GetString("fuelprices_area_department", area.Value),
            _ => area.Value
        };
    }

    /// <summary>
    /// Lowercases the name, removes its accents and turns everything but letters and digits into single spaces, so
    /// "saint etienne" matches "Saint-Étienne". It also keeps quotes and ODSQL wildcards out of the search.
    /// </summary>
    private static string NormalizeCityName(string name)
    {
        var withoutAccents = new StringBuilder();
        foreach (var character in name.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                withoutAccents.Append(character);
            }
        }

        return CITY_NAME_SEPARATORS_REGEX.Replace(withoutAccents.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodeRegex();

    [GeneratedRegex("^(\\d{1,2}|2[ab])$", RegexOptions.IgnoreCase)]
    private static partial Regex DepartmentCodeRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex CityNameSeparatorsRegex();
}
