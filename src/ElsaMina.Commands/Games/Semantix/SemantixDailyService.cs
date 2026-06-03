using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Semantix;

public class SemantixDailyService : ISemantixDailyService
{
    // Fixed reference day used to turn a calendar date into a stable index into the answers list.
    private static readonly DateOnly EPOCH = new(2024, 1, 1);

    private readonly IClockService _clockService;
    private readonly IDataManager _dataManager;
    private readonly IBotDbContextFactory _dbContextFactory;

    private HashSet<string> _validWords;

    public SemantixDailyService(IClockService clockService,
        IDataManager dataManager,
        IBotDbContextFactory dbContextFactory)
    {
        _clockService = clockService;
        _dataManager = dataManager;
        _dbContextFactory = dbContextFactory;
    }

    public DateOnly Today => DateOnly.FromDateTime(_clockService.CurrentUtcDateTime);

    public string GetDailyAnswer()
    {
        var answers = _dataManager.SemantixAnswersFr;
        if (answers == null || answers.Count == 0)
        {
            return null;
        }

        var dayNumber = Today.DayNumber - EPOCH.DayNumber;

        // Pseudo-shuffle the deterministic index so consecutive days don't walk
        // through the answers list in frequency order.
        var index = (int)((dayNumber * 2654435761L) % answers.Count);
        if (index < 0)
        {
            index += answers.Count;
        }

        return answers[index];
    }

    public bool IsValidWord(string word)
    {
        _validWords ??= _dataManager.SemantixWordsFr?.ToHashSet() ?? [];
        return _validWords.Contains(word);
    }

    public async Task<bool> HasWonTodayAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.SemantixScores
            .AsNoTracking()
            .FirstOrDefaultAsync(score => score.UserId == userId, cancellationToken);
        return record?.LastWonDate == Today;
    }
}
