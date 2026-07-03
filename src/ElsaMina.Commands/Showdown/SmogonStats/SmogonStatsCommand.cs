using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Smogon;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.SmogonStats;

[NamedCommand("smogonstats", Aliases = ["usage", "usagestats"])]
public class SmogonStatsCommand : Command
{
    private const int DEFAULT_PLAYER_LEVEL = 1760;
    private const int TOP_MOVES_COUNT = 10;
    private const int TOP_ITEMS_COUNT = 8;
    private const int TOP_ABILITIES_COUNT = 6;
    private const int TOP_TEAMMATES_COUNT = 10;

    private readonly ISmogonUsageDataProvider _smogonUsageDataProvider;
    private readonly ITemplatesManager _templatesManager;
    private readonly IClockService _clockService;

    public SmogonStatsCommand(ISmogonUsageDataProvider smogonUsageDataProvider,
        ITemplatesManager templatesManager,
        IClockService clockService)
    {
        _smogonUsageDataProvider = smogonUsageDataProvider;
        _templatesManager = templatesManager;
        _clockService = clockService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "smogonstats_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length < 2)
        {
            ReplyLocalizedHelpMessage(context, rankAware: true);
            return;
        }

        var pokemonName = parts[0].Trim();
        var format = parts[1].Trim().ToLower();
        var month = parts.Length > 2 ? parts[2].Trim() : GetDefaultMonth();
        var playerLevel = DEFAULT_PLAYER_LEVEL;

        if (parts.Length > 3 && int.TryParse(parts[3].Trim(), out var parsedLevel))
        {
            playerLevel = parsedLevel;
        }

        try
        {
            var usageData = await _smogonUsageDataProvider.GetUsageDataAsync(month, format, playerLevel, cancellationToken);

            var matchedKey = usageData.Data.Keys
                .FirstOrDefault(key => key.Equals(pokemonName, StringComparison.OrdinalIgnoreCase));

            if (matchedKey == null)
            {
                context.ReplyRankAwareLocalizedMessage("smogonstats_not_found", pokemonName, format);
                return;
            }

            var pokemonData = usageData.Data[matchedKey];
            var totalWeight = pokemonData.Abilities?.Values.Sum()
                ?? pokemonData.Items?.Values.Sum()
                ?? 1.0;

            var rank = usageData.Data
                .OrderByDescending(kvp => kvp.Value.Usage)
                .ToList()
                .FindIndex(kvp => kvp.Key == matchedKey) + 1;

            var viewModel = new SmogonStatsViewModel
            {
                Culture = context.Culture,
                PokemonName = matchedKey,
                Format = format,
                Month = month,
                PlayerLevel = playerLevel,
                Rank = rank,
                Usage = pokemonData.Usage * 100,
                RawCount = pokemonData.RawCount,
                TotalBattles = usageData.Info.NumberOfBattles,
                TopMoves = GetTopEntries(pokemonData.Moves, TOP_MOVES_COUNT, totalWeight),
                TopItems = GetTopEntries(pokemonData.Items, TOP_ITEMS_COUNT, totalWeight),
                TopAbilities = GetTopEntries(pokemonData.Abilities, TOP_ABILITIES_COUNT, totalWeight),
                TopTeammates = GetTopEntries(pokemonData.Teammates, TOP_TEAMMATES_COUNT, totalWeight)
            };

            var html = await _templatesManager.GetTemplateAsync("Showdown/SmogonStats/SmogonStats", viewModel);
            context.ReplyHtml(html.RemoveNewlines(), rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "SmogonStats command failed for {Pokemon} in {Format} ({Month}, {Level}+)",
                pokemonName, format, month, playerLevel);
            context.ReplyRankAwareLocalizedMessage("smogonstats_error");
        }
    }

    private static List<SmogonUsageEntry> GetTopEntries(Dictionary<string, double> entries,
        int count, double totalWeight)
    {
        if (entries == null || entries.Count == 0)
        {
            return [];
        }

        return entries
            .OrderByDescending(entry => entry.Value)
            .Take(count)
            .Select(entry => new SmogonUsageEntry(entry.Key, entry.Value / totalWeight * 100))
            .ToList();
    }

    private string GetDefaultMonth()
    {
        var lastMonth = _clockService.CurrentUtcDateTime.AddMonths(-1);
        return $"{lastMonth.Year:D4}-{lastMonth.Month:D2}";
    }
}
