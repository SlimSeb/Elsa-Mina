using System.Collections.Concurrent;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Smogon;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.Prediction;

public class SmogonOpponentMovesPredictor : IOpponentMovesPredictor
{
    private const int MAX_PREDICTED_MOVES = 6;
    private const int MOVES_PER_SET = 4;
    private const double MIN_CARRY_PROBABILITY = 0.1;

    // Smogon publishes chaos files for a handful of rating cutoffs; which ones exist depends on the format
    private static readonly int[] RATING_CUTOFFS = [1760, 1825, 1500, 0];

    private readonly ISmogonUsageDataProvider _smogonUsageDataProvider;
    private readonly IClockService _clockService;

    private readonly ConcurrentDictionary<string, Lazy<Task<SmogonUsageDataDto>>> _usageDataCache = new();

    public SmogonOpponentMovesPredictor(ISmogonUsageDataProvider smogonUsageDataProvider,
        IClockService clockService)
    {
        _smogonUsageDataProvider = smogonUsageDataProvider;
        _clockService = clockService;
    }

    public async Task<IReadOnlyList<PredictedMove>> PredictMovesAsync(string format, string species,
        IReadOnlyCollection<string> revealedMoves, CancellationToken cancellationToken = default)
    {
        var predictions = revealedMoves
            .Select(moveName => new PredictedMove(moveName, 1.0))
            .ToList();

        if (predictions.Count >= MOVES_PER_SET || string.IsNullOrWhiteSpace(format) ||
            string.IsNullOrWhiteSpace(species) || format.Contains("random", StringComparison.OrdinalIgnoreCase))
        {
            return predictions;
        }

        var usageData = await GetUsageDataAsync(format);
        var pokemonData = FindPokemonData(usageData, species);
        if (pokemonData?.Moves == null || pokemonData.Moves.Count == 0)
        {
            return predictions;
        }

        var totalWeight = pokemonData.Abilities?.Values.Sum()
                          ?? pokemonData.Items?.Values.Sum()
                          ?? 0.0;
        if (totalWeight <= 0)
        {
            return predictions;
        }

        var revealedNames = new HashSet<string>(revealedMoves, StringComparer.OrdinalIgnoreCase);
        var usageMoves = pokemonData.Moves
            .Where(move => !revealedNames.Contains(move.Key))
            .Select(move => new PredictedMove(move.Key, Math.Min(1.0, move.Value / totalWeight)))
            .Where(move => move.Probability >= MIN_CARRY_PROBABILITY)
            .OrderByDescending(move => move.Probability)
            .Take(MAX_PREDICTED_MOVES - predictions.Count);

        predictions.AddRange(usageMoves);
        return predictions;
    }

    private static SmogonPokemonUsageDataDto FindPokemonData(SmogonUsageDataDto usageData, string species)
    {
        if (usageData?.Data == null)
        {
            return null;
        }

        var matchedKey = usageData.Data.Keys
            .FirstOrDefault(key => key.Equals(species, StringComparison.OrdinalIgnoreCase));
        return matchedKey == null ? null : usageData.Data[matchedKey];
    }

    private Task<SmogonUsageDataDto> GetUsageDataAsync(string format)
    {
        var month = GetLatestPublishedMonth();
        var cacheKey = $"{format}:{month}";

        // Lazy dedupes concurrent fetches; failures are cached as a null result so a format
        // without published stats (or a Smogon outage) is not retried on every single turn.
        var lazyFetch = _usageDataCache.GetOrAdd(cacheKey,
            _ => new Lazy<Task<SmogonUsageDataDto>>(() => FetchUsageDataAsync(format, month)));
        return lazyFetch.Value;
    }

    private async Task<SmogonUsageDataDto> FetchUsageDataAsync(string format, string firstMonth)
    {
        // Stats for a month are published a few days into the next one, so also try one month further back
        var months = new[] { firstMonth, GetPreviousMonth(firstMonth) };
        foreach (var month in months)
        {
            foreach (var ratingCutoff in RATING_CUTOFFS)
            {
                try
                {
                    var usageData = await _smogonUsageDataProvider.GetUsageDataAsync(month, format, ratingCutoff);
                    if (usageData?.Data is { Count: > 0 })
                    {
                        Log.Information("Loaded Smogon usage data for {Format} ({Month}, {Rating}+)",
                            format, month, ratingCutoff);
                        return usageData;
                    }
                }
                catch (Exception exception)
                {
                    Log.Debug("No Smogon usage data for {Format} ({Month}, {Rating}+): {Message}",
                        format, month, ratingCutoff, exception.Message);
                }
            }
        }

        Log.Information("Could not load any Smogon usage data for format {Format}", format);
        return null;
    }

    private string GetLatestPublishedMonth()
    {
        var lastMonth = _clockService.CurrentUtcDateTime.AddMonths(-1);
        return $"{lastMonth.Year:D4}-{lastMonth.Month:D2}";
    }

    private static string GetPreviousMonth(string month)
    {
        var parts = month.Split('-');
        var date = new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), 1).AddMonths(-1);
        return $"{date.Year:D4}-{date.Month:D2}";
    }
}
