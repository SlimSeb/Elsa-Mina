namespace ElsaMina.Sheets;

public interface ISheet : IDisposable
{
    string Name { get; }
    Task<string?> GetCellAsync(int column, int row, CancellationToken cancellationToken = default);
    Task SetCellAsync(int column, int row, string? content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRowAsync(int row, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetColumnAsync(int column, CancellationToken cancellationToken = default);
    Task FlushAsync(CancellationToken cancellationToken = default);
}