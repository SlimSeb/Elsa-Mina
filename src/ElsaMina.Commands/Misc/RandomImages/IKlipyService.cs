namespace ElsaMina.Commands.Misc.RandomImages;

public interface IKlipyService
{
    Task<GifMediaInfo> GetRandomMediaAsync(string query, KlipyMediaSize size, KlipyMediaFormat format,
        CancellationToken cancellationToken = default);

    Task<List<GifSearchResult>> SearchAsync(string query, int count,
        CancellationToken cancellationToken = default);
}
