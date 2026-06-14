using System.Globalization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleDailyService : IWordleDailyService
{
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

    public DateOnly GetToday(TimeZoneInfo timeZone)
    {
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(_clockService.CurrentUtcDateTime,
            timeZone ?? TimeZoneInfo.Utc);
        return DateOnly.FromDateTime(localDateTime);
    }

    public IReadOnlyList<string> GetWords(CultureInfo culture)
    {
        if (culture?.TwoLetterISOLanguageName == "fr" && _dataManager.WordleWordsFr is { Count: > 0 })
        {
            return _dataManager.WordleWordsFr;
        }

        return _dataManager.WordleWords ?? [];
    }

    public string GetDailyAnswer(CultureInfo culture, TimeZoneInfo timeZone)
    {
        var words = GetWords(culture);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        // Seed the picker with the current calendar day in the room timezone so the answer is the
        // same for every player in that room and only changes once a day.
        var index = new Random(GetToday(timeZone).DayNumber).Next(words.Count);
        return words[index].ToUpperInvariant();
    }

    public async Task<bool> HasPlayedTodayAsync(string userId, TimeZoneInfo timeZone,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.WordleScores
            .AsNoTracking()
            .FirstOrDefaultAsync(score => score.UserId == userId, cancellationToken);
        return record?.LastPlayedDate == GetToday(timeZone);
    }
}
