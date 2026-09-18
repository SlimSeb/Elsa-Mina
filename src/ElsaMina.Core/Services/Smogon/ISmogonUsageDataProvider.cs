namespace ElsaMina.Core.Services.Smogon;

public interface ISmogonUsageDataProvider
{
    Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, Level playerLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renvoie le classement d'utilisation du mois, sans les détails (moves, items...).
    /// Beaucoup plus léger que <see cref="GetUsageDataAsync"/> : quelques dizaines de Ko contre
    /// plusieurs Mo, donc utilisable pour agréger plusieurs mois d'affilée.
    /// </summary>
    Task<IReadOnlyList<SmogonUsageRankingEntryDto>> GetUsageRankingAsync(string month, string format,
        Level playerLevel, CancellationToken cancellationToken = default);
}
