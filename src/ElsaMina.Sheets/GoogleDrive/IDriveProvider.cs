namespace ElsaMina.Sheets.GoogleDrive;

public interface IDriveProvider : IDisposable
{
    /// <summary>
    /// Finds a shared drive by its exact name. Returns null when no shared drive matches.
    /// </summary>
    Task<string?> FindSharedDriveIdAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a folder by its exact name, optionally within a given parent. Returns null when not found.
    /// </summary>
    Task<string?> FindFolderIdAsync(string name, string? parentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the children of a folder or of a shared drive root, optionally filtered by mime type.
    /// </summary>
    Task<IReadOnlyList<DriveItem>> ListChildrenAsync(string parentId, string? mimeType = null,
        CancellationToken cancellationToken = default);
}
