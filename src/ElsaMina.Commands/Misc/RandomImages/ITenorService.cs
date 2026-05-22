namespace ElsaMina.Commands.Misc.RandomImages;

public interface ITenorService
{
    Task<TenorMediaInfo> GetRandomMediaAsync(string query, string mediaFormat,
        CancellationToken cancellationToken = default);

    Task<List<TenorMediaInfo>> GetMultipleMediaAsync(string query, string mediaFormat, int count,
        CancellationToken cancellationToken = default);
}
