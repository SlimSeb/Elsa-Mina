using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace ElsaMina.Sheets.GoogleSheets;

public class GoogleSheet : ISheet
{
    private const string USER_ENTERED_INPUT_OPTION = "USER_ENTERED";
    private readonly SheetsService _service;
    private readonly string _spreadsheetId;
    private readonly string _rangePrefix;
    private readonly List<List<string>> _rows;
    private readonly List<(int Column, int Row, string? Value)> _pendingUpdates = [];
    private bool _disposed;

    public GoogleSheet(
        SheetsService service,
        string spreadsheetId,
        string sheetName,
        List<List<string>> initialRows)
    {
        _service = service;
        _spreadsheetId = spreadsheetId;
        Name = sheetName;

        _rangePrefix = $"{sheetName}!";

        _rows = initialRows;
    }

    public string Name { get; }

    public Task<string?> GetCellAsync(int column, int row, CancellationToken cancellationToken = default)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return Task.FromResult<string?>(null);
        }

        if (column < 0 || column >= _rows[row].Count)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(_rows[row][column]);
    }

    public Task<IReadOnlyList<string>> GetRowAsync(int row, CancellationToken cancellationToken = default)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult<IReadOnlyList<string>>(_rows[row]);
    }

    public Task<IReadOnlyList<string>> GetColumnAsync(int column, CancellationToken cancellationToken = default)
    {
        if (column < 0)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var result = new List<string>(_rows.Count);
        result.AddRange(_rows.Select(row => column < row.Count ? row[column] : string.Empty));

        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    public Task SetCellAsync(int column, int row, string? content, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        while (_rows.Count <= row)
        {
            _rows.Add([]);
        }

        while (_rows[row].Count <= column)
        {
            _rows[row].Add(string.Empty);
        }

        _rows[row][column] = content ?? string.Empty;

        _pendingUpdates.Add((column, row, content));

        return Task.CompletedTask;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingUpdates.Count == 0)
        {
            return;
        }

        var data = _pendingUpdates
            .Select(update => new ValueRange
            {
                Range = _rangePrefix + ToA1(update.Column, update.Row),
                Values = new List<IList<object?>>
                {
                    new object?[] { update.Value }
                }
            }).ToList();

        var batch = new BatchUpdateValuesRequest
        {
            Data = data,
            ValueInputOption = USER_ENTERED_INPUT_OPTION
        };

        var request = _service.Spreadsheets.Values.BatchUpdate(batch, _spreadsheetId);
        await request.ExecuteAsync(cancellationToken);

        _pendingUpdates.Clear();
    }

    private static string ToA1(int column, int row)
    {
        var col = "";
        var dividend = column + 1;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            col = Convert.ToChar('A' + modulo) + col;
            dividend = (dividend - modulo) / 26;
        }

        return $"{col}{row + 1}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        if (_pendingUpdates.Count > 0)
        {
            FlushAsync().GetAwaiter().GetResult();
        }

        _pendingUpdates.Clear();
        _rows.Clear();
        _disposed = true;
    }

    ~GoogleSheet()
    {
        Dispose(false);
    }
}