namespace ElsaMina.Core.Services.Smogon;

public interface ISmogonUsageDataProvider
{
    Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, Level playerLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the month's usage ranking, without the details (moves, items...).
    /// Much lighter than <see cref="GetUsageDataAsync"/>: a few dozen KB instead of
    /// several MB, so it can be used to aggregate several months in a row.
    /// </summary>
    Task<IReadOnlyList<SmogonUsageRankingEntryDto>> GetUsageRankingAsync(string month, string format,
        Level playerLevel, CancellationToken cancellationToken = default);
}
