namespace ElsaMina.Cloud.Sheets;

public interface ISheetProvider : IDisposable
{
    Task<ISheet> GetSheetAsync(string spreadsheetName, string sheetName, CancellationToken cancellationToken = default);
}
