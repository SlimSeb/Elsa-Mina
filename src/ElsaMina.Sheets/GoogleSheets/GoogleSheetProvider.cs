using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace ElsaMina.Sheets.GoogleSheets;

public class GoogleSheetProvider : ISheetProvider
{
    private readonly SheetsService _sheets;
    private readonly DriveService _drive;

    public GoogleSheetProvider(GoogleCredential credential)
    {
        _sheets = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential
        });

        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential
        });
    }

    public async Task<ISheet> GetSheetAsync(string spreadsheetName, string sheetName,
        CancellationToken cancellationToken = default)
    {
        if (_sheets == null || _drive == null)
        {
            throw new InvalidOperationException("Service not initialized.");
        }

        var spreadsheetId = await FindSpreadsheetIdAsync(spreadsheetName, cancellationToken);
        var spreadsheet = await _sheets.Spreadsheets.Get(spreadsheetId).ExecuteAsync(cancellationToken);
        var sheet = spreadsheet.Sheets.First(sht => sht.Properties.Title == sheetName);

        var rowCount = sheet.Properties.GridProperties.RowCount ?? 1000;
        var colCount = sheet.Properties.GridProperties.ColumnCount ?? 26;

        var lastCol = ToA1Column(colCount - 1);
        var fullRange = $"{sheetName}!A1:{lastCol}{rowCount}";

        var response = await _sheets.Spreadsheets.Values
            .Get(spreadsheetId, fullRange)
            .ExecuteAsync(cancellationToken);

        var values = response.Values ?? new List<IList<object>>();
        var rows = new List<List<string>>(rowCount);

        for (var r = 0; r < rowCount; r++)
        {
            var row = new List<string>(colCount);

            for (var c = 0; c < colCount; c++)
            {
                if (r < values.Count && c < values[r].Count)
                    row.Add(values[r][c]?.ToString() ?? string.Empty);
                else
                    row.Add(string.Empty); // padding des cellules manquantes
            }

            rows.Add(row);
        }

        return new GoogleSheet(_sheets, spreadsheetId, sheetName, rows);
    }
    
    private static string ToA1Column(int column)
    {
        var colBuilder = new StringBuilder();
        column += 1;
        while (column > 0)
        {
            var mod = (column - 1) % 26;
            colBuilder.Insert(0, (char)('A' + mod));
            column = (column - mod) / 26;
        }
        return colBuilder.ToString();
    }

    /// <summary>
    /// Récupérer l'ID d'une spreadsheet via son nom Google Drive
    /// </summary>
    private async Task<string> FindSpreadsheetIdAsync(string name, CancellationToken cancellationToken = default)
    {
        var request = _drive.Files.List();
        request.Q = $"name = '{name}' and mimeType = 'application/vnd.google-apps.spreadsheet'";
        request.Fields = "files(id, name)";

        var response = await request.ExecuteAsync(cancellationToken);

        if (response.Files.Count == 0)
        {
            throw new FileNotFoundException($"Spreadsheet '{name}' not found.");
        }

        return response.Files[0].Id;
    }

    public void Dispose()
    {
        _sheets.Dispose();
        _drive.Dispose();
    }
}
