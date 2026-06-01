using System.Globalization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleDailyService : IWordleDailyService
{
    // Fixed reference day used to turn a calendar date into a stable index into the word list.
    private static readonly DateOnly EPOCH = new(2024, 1, 1);

    private readonly IClockService _clockService;
    private readonly IDataManager _dataManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    public WordleDailyService(IClockService clockService,
        IDataManager dataManager,
        IBotDbContextFactory dbContextFactory)
    {
        _clockService = clockService;
        _dataManager = dataManager;
        _dbContextFactory = dbContextFactory;
    }

    public DateOnly Today => DateOnly.FromDateTime(_clockService.CurrentUtcDateTime);

    public IReadOnlyList<string> GetWords(CultureInfo culture)
    {
        if (culture?.TwoLetterISOLanguageName == "fr" && _dataManager.WordleWordsFr is { Count: > 0 })
        {
            return _dataManager.WordleWordsFr;
        }

        return _dataManager.WordleWords ?? [];
    }

    public string GetDailyAnswer(CultureInfo culture)
    {
        var words = GetWords(culture);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var dayNumber = Today.DayNumber - EPOCH.DayNumber;
        var index = ((dayNumber % words.Count) + words.Count) % words.Count;
        return words[index].ToUpperInvariant();
    }

    public async Task<bool> HasPlayedTodayAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.WordleScores
            .AsNoTracking()
            .FirstOrDefaultAsync(score => score.UserId == userId, cancellationToken);
        return record?.LastPlayedDate == Today;
    }
}
