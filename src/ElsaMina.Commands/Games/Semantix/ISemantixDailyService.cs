namespace ElsaMina.Commands.Games.Semantix;

public interface ISemantixDailyService
{
    DateOnly Today { get; }
    string GetDailyAnswer();
    bool IsValidWord(string word);
    Task<bool> HasWonTodayAsync(string userId, CancellationToken cancellationToken = default);
}
