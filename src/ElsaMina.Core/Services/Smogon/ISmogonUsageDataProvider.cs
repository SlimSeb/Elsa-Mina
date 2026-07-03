namespace ElsaMina.Core.Services.Smogon;

public interface ISmogonUsageDataProvider
{
    Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, int playerLevel,
        CancellationToken cancellationToken = default);
}
