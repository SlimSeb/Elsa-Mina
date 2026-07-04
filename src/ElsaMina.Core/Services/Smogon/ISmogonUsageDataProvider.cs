namespace ElsaMina.Core.Services.Smogon;

public interface ISmogonUsageDataProvider
{
    Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, Level playerLevel,
        CancellationToken cancellationToken = default);
}
