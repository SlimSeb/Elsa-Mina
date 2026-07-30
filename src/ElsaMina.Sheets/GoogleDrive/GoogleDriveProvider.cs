using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace ElsaMina.Sheets.GoogleDrive;

public sealed class GoogleDriveProvider : IDriveProvider
{
    public const string FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";

    private const int PAGE_SIZE = 1000;

    private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(30);

    private readonly DriveService _drive;

    public GoogleDriveProvider(GoogleCredential credential)
    {
        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential
        });
        // Without this the client waits on its default timeout and retries, which looks like a hang from chat.
        _drive.HttpClient.Timeout = REQUEST_TIMEOUT;
    }

    /// <summary>
    /// Only returns drives the bot's service account is a member of: sharing a folder with the account
    /// is not enough, the account has to be a member of the shared drive itself.
    /// </summary>
    public async Task<string?> FindSharedDriveIdAsync(string name, CancellationToken cancellationToken = default)
    {
        // Matching client side, because the drives endpoint only supports "contains" and accented
        // names do not always match the way the caller expects.
        var request = _drive.Drives.List();
        request.Fields = "drives(id, name)";
        request.PageSize = 100;

        var response = await request.ExecuteAsync(cancellationToken);
        return response.Drives?
            .FirstOrDefault(drive => string.Equals(drive.Name, name, StringComparison.OrdinalIgnoreCase))?
            .Id;
    }

    public async Task<string?> FindFolderIdAsync(string name, string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"name = '{Escape(name)}' and mimeType = '{FOLDER_MIME_TYPE}' and trashed = false";
        if (!string.IsNullOrEmpty(parentId))
        {
            query += $" and '{Escape(parentId)}' in parents";
        }

        var request = _drive.Files.List();
        request.Q = query;
        request.Fields = "files(id, name)";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;

        var response = await request.ExecuteAsync(cancellationToken);
        return response.Files?.FirstOrDefault()?.Id;
    }

    public async Task<IReadOnlyList<DriveItem>> ListChildrenAsync(string parentId, string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"'{Escape(parentId)}' in parents and trashed = false";
        if (!string.IsNullOrEmpty(mimeType))
        {
            query += $" and mimeType = '{Escape(mimeType)}'";
        }

        var items = new List<DriveItem>();
        string? pageToken = null;
        do
        {
            var request = _drive.Files.List();
            request.Q = query;
            request.Fields = "nextPageToken, files(id, name)";
            request.OrderBy = "name";
            request.PageSize = PAGE_SIZE;
            request.PageToken = pageToken;
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;

            var response = await request.ExecuteAsync(cancellationToken);
            if (response.Files != null)
            {
                items.AddRange(response.Files.Select(file => new DriveItem(file.Id, file.Name)));
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return items;
    }

    private static string Escape(string value)
    {
        return value.Replace(@"\", @"\\").Replace("'", @"\'");
    }

    public void Dispose()
    {
        _drive.Dispose();
    }
}
