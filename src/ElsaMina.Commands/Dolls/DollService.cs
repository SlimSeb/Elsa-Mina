using System.Text.RegularExpressions;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using ElsaMina.Sheets.GoogleDrive;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Dolls;

public partial class DollService : IDollService
{
    private const string DEFAULT_DRIVE_NAME = "Poupées";
    private const string PNG_MIME_TYPE = "image/png";

    /// <summary>
    /// Position of a doll the user never moved: it stays on its default place on the shelf.
    /// </summary>
    private const int UNPLACED_POSITION = 0;

    /// <summary>
    /// Direct image endpoint for a Drive file. The files must be shared with "anyone with the link",
    /// otherwise Showdown clients cannot load them.
    /// </summary>
    private const string IMAGE_URL_FORMAT = "https://lh3.googleusercontent.com/d/{0}";

    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

    private readonly IDriveProvider _driveProvider;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IClockService _clockService;

    private readonly SemaphoreSlim _catalogueLock = new(1, 1);
    private IReadOnlyDictionary<string, Doll> _catalogue;
    private DateTime _catalogueFetchDate;

    public DollService(IDriveProvider driveProvider,
        IBotDbContextFactory dbContextFactory,
        IConfiguration configuration,
        IClockService clockService)
    {
        _driveProvider = driveProvider;
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _clockService = clockService;
    }

    public async Task<IReadOnlyDictionary<string, Doll>> GetCatalogueAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsCacheValid())
        {
            return _catalogue;
        }

        await _catalogueLock.WaitAsync(cancellationToken);
        try
        {
            return IsCacheValid() ? _catalogue : await FetchAndCacheCatalogueAsync(cancellationToken);
        }
        finally
        {
            _catalogueLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, Doll>> 
        RefreshCatalogueAsync(
        CancellationToken cancellationToken = default)
    {
        await _catalogueLock.WaitAsync(cancellationToken);
        try
        {
            return await FetchAndCacheCatalogueAsync(cancellationToken);
        }
        finally
        {
            _catalogueLock.Release();
        }
    }

    public async Task<Doll> GetDollAsync(string dollId, CancellationToken cancellationToken = default)
    {
        var catalogue = await GetCatalogueAsync(cancellationToken);
        return catalogue.GetValueOrDefault(dollId);
    }

    public async Task<IReadOnlyList<Doll>> ResolveDollsAsync(IEnumerable<DollHolding> holdings,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, Doll> catalogue;
        try
        {
            catalogue = await GetCatalogueAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not read the doll catalogue");
            return [];
        }

        return OrderForShelf(holdings, catalogue)
            .Select(holding => catalogue.GetValueOrDefault(holding.DollId))
            .Where(doll => doll != null)
            .ToList();
    }

    public async Task<DollMoveResult> MoveDollAsync(string roomId, string userId, string dollId, int offset,
        CancellationToken cancellationToken = default)
    {
        var catalogue = await GetCatalogueAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var holdings = await dbContext.DollHoldings
            .Where(holding => holding.UserId == userId && holding.RoomId == roomId)
            .ToListAsync(cancellationToken);

        var shelf = OrderForShelf(holdings, catalogue).ToList();
        var currentIndex = shelf.FindIndex(holding => holding.DollId == dollId);
        if (currentIndex == -1)
        {
            return DollMoveResult.NotOwned;
        }

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= shelf.Count)
        {
            return DollMoveResult.AlreadyAtEdge;
        }

        (shelf[currentIndex], shelf[targetIndex]) = (shelf[targetIndex], shelf[currentIndex]);

        // The whole shelf is renumbered, so the dolls that were still on their default place keep
        // the one they were displayed at rather than jumping around on the next move.
        for (var i = 0; i < shelf.Count; i++)
        {
            shelf[i].Position = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return DollMoveResult.Moved;
    }

    /// <summary>
    /// Dolls the user placed come first, in their chosen order, then the ones left on their default
    /// place, biggest first, so that a shelf that was never reordered looks exactly as it always did.
    /// </summary>
    private static IEnumerable<DollHolding> OrderForShelf(IEnumerable<DollHolding> holdings,
        IReadOnlyDictionary<string, Doll> catalogue)
    {
        return holdings
            .OrderBy(holding => holding.Position == UNPLACED_POSITION ? int.MaxValue : holding.Position)
            .ThenByDescending(holding => catalogue.GetValueOrDefault(holding.DollId)?.Size ?? 0)
            .ThenBy(holding => catalogue.GetValueOrDefault(holding.DollId)?.Name ?? holding.DollId);
    }

    public async Task<bool> IsDollOwnedByUserAsync(string roomId, string userId, string dollId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.DollHoldings.AnyAsync(
            holding => holding.DollId == dollId && holding.UserId == userId && holding.RoomId == roomId,
            cancellationToken);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{15,}$", RegexOptions.Compiled)]
    private static partial Regex DriveIdRegex();

    private bool IsCacheValid()
    {
        return _catalogue != null && _clockService.CurrentUtcDateTime - _catalogueFetchDate < CACHE_DURATION;
    }

    private async Task<IReadOnlyDictionary<string, Doll>> FetchAndCacheCatalogueAsync(
        CancellationToken cancellationToken)
    {
        var catalogue = await FetchCatalogueAsync(cancellationToken);
        _catalogue = catalogue;
        _catalogueFetchDate = _clockService.CurrentUtcDateTime;
        return catalogue;
    }

    /// <summary>
    /// Resolves what <see cref="IConfiguration.DollsDriveName"/> points at, cheapest and most reliable first.
    /// </summary>
    private async Task<string> ResolveContainerIdAsync(string driveName, CancellationToken cancellationToken)
    {
        // A folder id pasted from the Drive url needs no lookup at all, so it wins when configured.
        if (DriveIdRegex().IsMatch(driveName))
        {
            return driveName;
        }

        // Finds a folder shared with the bot, whether it sits in a My Drive or inside a shared drive.
        var folderId = await _driveProvider.FindFolderIdAsync(driveName, cancellationToken: cancellationToken);
        if (folderId != null)
        {
            return folderId;
        }

        // Last resort: the root of a shared drive is not a folder, so it can only be found this way.
        // It stays non fatal because it needs the service account to be a member of the drive.
        try
        {
            return await _driveProvider.FindSharedDriveIdAsync(driveName, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not list the shared drives");
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, Doll>> FetchCatalogueAsync(CancellationToken cancellationToken)
    {
        var driveName = string.IsNullOrWhiteSpace(_configuration.DollsDriveName)
            ? DEFAULT_DRIVE_NAME
            : _configuration.DollsDriveName;

        var containerId = await ResolveContainerIdAsync(driveName, cancellationToken);
        if (containerId == null)
        {
            throw new InvalidOperationException(
                $"Could not find the drive or folder '{driveName}'. Share it with the bot's service account, " +
                "or configure DollsDriveName with the folder id taken from its Drive url.");
        }

        var folders = await _driveProvider.ListChildrenAsync(containerId, GoogleDriveProvider.FOLDER_MIME_TYPE,
            cancellationToken);

        var dolls = new Dictionary<string, Doll>();
        foreach (var folder in folders)
        {
            // Any folder whose name carries a pixel size becomes a size tier, so new tiers need no code change.
            if (!DollCatalogueNaming.TryParseSize(folder.Name, out var size))
            {
                Log.Warning("Skipping doll folder {FolderName}: no size in its name", folder.Name);
                continue;
            }

            var files = await _driveProvider.ListChildrenAsync(folder.Id, PNG_MIME_TYPE, cancellationToken);
            foreach (var file in files)
            {
                var dollId = DollCatalogueNaming.ToDollId(file.Name);
                if (string.IsNullOrEmpty(dollId) || dolls.ContainsKey(dollId))
                {
                    continue;
                }

                dolls[dollId] = new Doll
                {
                    Id = dollId,
                    Name = DollCatalogueNaming.ToDisplayName(file.Name),
                    Size = size,
                    Image = string.Format(IMAGE_URL_FORMAT, file.Id)
                };
            }
        }

        Log.Information("Loaded {DollCount} dolls from the drive '{DriveName}'", dolls.Count, driveName);
        return dolls;
    }
}
