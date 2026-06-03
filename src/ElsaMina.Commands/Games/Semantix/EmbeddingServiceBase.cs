using System.Collections.Concurrent;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.Semantix;

/// <summary>
/// Embedding pipeline shared by all providers: in-memory cache, then permanent
/// database cache (keyed by word + model), then the provider API with a daily
/// call budget. Vectors from different models are never mixed.
/// </summary>
public abstract class EmbeddingServiceBase : IEmbeddingService
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IClockService _clockService;

    private readonly ConcurrentDictionary<string, float[]> _memoryCache = new();

    private readonly Lock _budgetLock = new();
    private DateOnly _budgetDate;
    private int _apiCallsToday;

    protected EmbeddingServiceBase(IBotDbContextFactory dbContextFactory, IClockService clockService)
    {
        _dbContextFactory = dbContextFactory;
        _clockService = clockService;
    }

    protected abstract string ModelName { get; }
    protected abstract Task<float[]> FetchFromApiAsync(string word, CancellationToken cancellationToken);

    public async Task<float[]> GetEmbeddingAsync(string word, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return null;
        }

        var normalized = word.Trim().ToLowerInvariant();

        if (_memoryCache.TryGetValue(normalized, out var cachedVector))
        {
            return cachedVector;
        }

        var fromDatabase = await TryGetFromDatabaseAsync(normalized, cancellationToken);
        if (fromDatabase != null)
        {
            _memoryCache[normalized] = fromDatabase;
            return fromDatabase;
        }

        if (!TryConsumeApiBudget())
        {
            Log.Warning("Semantix daily embedding API budget exhausted.");
            return null;
        }

        var fromApi = await FetchFromApiAsync(normalized, cancellationToken);
        if (fromApi == null)
        {
            return null;
        }

        _memoryCache[normalized] = fromApi;
        await TrySaveToDatabaseAsync(normalized, fromApi, cancellationToken);
        return fromApi;
    }

    private async Task<float[]> TryGetFromDatabaseAsync(string word, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var record = await dbContext.WordEmbeddings.FindAsync([word, ModelName], cancellationToken);
            return record == null ? null : SemantixMath.DeserializeVector(record.Vector);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to read embedding cache for word {0}", word);
            return null;
        }
    }

    private async Task TrySaveToDatabaseAsync(string word, float[] vector, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await dbContext.WordEmbeddings.FindAsync([word, ModelName], cancellationToken);
            if (existing != null)
            {
                return;
            }

            await dbContext.WordEmbeddings.AddAsync(new WordEmbedding
            {
                Word = word,
                Model = ModelName,
                Vector = SemantixMath.SerializeVector(vector)
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save embedding cache for word {0}", word);
        }
    }

    private bool TryConsumeApiBudget()
    {
        lock (_budgetLock)
        {
            var today = DateOnly.FromDateTime(_clockService.CurrentUtcDateTime);
            if (_budgetDate != today)
            {
                _budgetDate = today;
                _apiCallsToday = 0;
            }

            if (_apiCallsToday >= SemantixConstants.MAX_DAILY_API_CALLS)
            {
                return false;
            }

            _apiCallsToday++;
            return true;
        }
    }
}
