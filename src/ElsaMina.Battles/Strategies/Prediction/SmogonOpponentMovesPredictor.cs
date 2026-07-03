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

    public async Task<OpponentPrediction> PredictAsync(string format, string species,
        IReadOnlyCollection<string> revealedMoves, CancellationToken cancellationToken = default)
    {
        var predictedMoves = revealedMoves
            .Select(moveName => new PredictedMove(moveName, 1.0))
            .ToList();

        if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(species) ||
            format.Contains("random", StringComparison.OrdinalIgnoreCase))
        {
            return new OpponentPrediction(predictedMoves, null);
        }

        var usageData = await GetUsageDataAsync(format);
        var pokemonData = FindPokemonData(usageData, species);
        if (pokemonData == null)
        {
            return new OpponentPrediction(predictedMoves, null);
        }

        var totalWeight = pokemonData.Abilities?.Values.Sum()
                          ?? pokemonData.Items?.Values.Sum()
                          ?? 0.0;

        AddUsageMoves(predictedMoves, pokemonData, revealedMoves, totalWeight);
        var spread = ParseMostCommonSpread(pokemonData);
        return new OpponentPrediction(predictedMoves, spread);
    }

    private static void AddUsageMoves(List<PredictedMove> predictedMoves,
        SmogonPokemonUsageDataDto pokemonData, IReadOnlyCollection<string> revealedMoves, double totalWeight)
    {
        // Once four moves are already revealed the set is known, so usage moves add nothing
        if (predictedMoves.Count >= MOVES_PER_SET || pokemonData.Moves == null ||
            pokemonData.Moves.Count == 0 || totalWeight <= 0)
        {
            return;
        }

        var revealedNames = new HashSet<string>(revealedMoves, StringComparer.OrdinalIgnoreCase);
        var usageMoves = pokemonData.Moves
            .Where(move => !revealedNames.Contains(move.Key))
            .Select(move => new PredictedMove(move.Key, Math.Min(1.0, move.Value / totalWeight)))
            .Where(move => move.Probability >= MIN_CARRY_PROBABILITY)
            .OrderByDescending(move => move.Probability)
            .Take(MAX_PREDICTED_MOVES - predictedMoves.Count);

        predictedMoves.AddRange(usageMoves);
    }

    // Spread keys look like "Jolly:0/252/0/0/4/252" => Nature:HP/Atk/Def/SpA/SpD/Spe
    private static PredictedSpread ParseMostCommonSpread(SmogonPokemonUsageDataDto pokemonData)
    {
        if (pokemonData.Spreads == null || pokemonData.Spreads.Count == 0)
        {
            return null;
        }

        var mostCommon = pokemonData.Spreads
            .OrderByDescending(spread => spread.Value)
            .First().Key;

        var natureAndEvs = mostCommon.Split(':');
        if (natureAndEvs.Length != 2)
        {
            return null;
        }

        var evTokens = natureAndEvs[1].Split('/');
        if (evTokens.Length != 6)
        {
            return null;
        }

        var evs = new int[6];
        for (var index = 0; index < 6; index++)
        {
            if (!int.TryParse(evTokens[index], out evs[index]))
            {
                return null;
            }
        }

        return new PredictedSpread(natureAndEvs[0], evs[0], evs[1], evs[2], evs[3], evs[4], evs[5]);
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
