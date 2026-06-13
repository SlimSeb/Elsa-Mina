using System.Globalization;

namespace ElsaMina.Commands.Games.Wordle;

public interface IWordleDailyService
{
    DateOnly Today { get; }

    /// <summary>
    /// The word list for the given culture (French when the culture is French, English otherwise).
    /// </summary>
    IReadOnlyList<string> GetWords(CultureInfo culture);

    /// <summary>
    /// The answer of the day, identical for every player of the same language in the same room and
    /// changing once per day, where the day boundary is computed in the given timezone.
    /// </summary>
    string GetDailyAnswer(CultureInfo culture, TimeZoneInfo timeZone);

    Task<bool> HasPlayedTodayAsync(string userId, CancellationToken cancellationToken = default);
}
