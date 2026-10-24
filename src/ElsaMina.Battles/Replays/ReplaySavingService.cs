using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Replays;

/// <summary>
/// Saves each battle the bot plays, once over, as two files named after Showdown's replay id:
/// <list type="bullet">
/// <item>{id}.log: every line the bot received in the battle room. Unlike the replay, it has our |request| lines
/// (our team's moves, stats and items), which minipoke needs to rebuild what the bot saw.</item>
/// <item>{id}.json: the replay Showdown uploads on /savereplay (log, players, rating, upload time), downloaded once
/// the server's popup gives its address.</item>
/// </list>
/// </summary>
public class ReplaySavingService : IReplaySavingService
{
    public const int MAX_DOWNLOAD_ATTEMPTS = 3;
    public static readonly TimeSpan DOWNLOAD_RETRY_DELAY = TimeSpan.FromSeconds(10);

    // Showdown answers /savereplay within seconds: without a popup by then, the upload failed
    public static readonly TimeSpan UPLOAD_TIMEOUT = TimeSpan.FromMinutes(2);

    private const string BATTLE_ROOM_PREFIX = "battle-";
    private const string HIDDEN_ROOM_SUFFIX = "pw";

    private readonly IBot _bot;
    private readonly IHttpService _httpService;
    private readonly IClockService _clockService;
    private readonly ISystemService _systemService;
    private readonly string _directory;
    private readonly Lock _lock = new();

    // Replay id -> when the bot asked Showdown to upload it
    private readonly Dictionary<string, DateTimeOffset> _uploadRequests = [];

    private bool _isSaving;
    private int _downloadCount;
    private int _savedCount;
    private int _failedCount;
    private string _lastSaved;

    public ReplaySavingService(IBot bot, IHttpService httpService, IClockService clockService,
        ISystemService systemService, IBattlesConfiguration configuration)
    {
        _bot = bot;
        _httpService = httpService;
        _clockService = clockService;
        _systemService = systemService;
        _directory = string.IsNullOrWhiteSpace(configuration.ReplaysDirectory)
            ? Path.Join(AppContext.BaseDirectory, "replays")
            : Path.GetFullPath(configuration.ReplaysDirectory);
    }

    public ReplaySavingStatus Status
    {
        get
        {
            lock (_lock)
            {
                ExpireUploadRequests();
                return new ReplaySavingStatus(_isSaving, _directory, _savedCount, _failedCount,
                    _uploadRequests.Count + _downloadCount, _lastSaved);
            }
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            _isSaving = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isSaving = false;
        }
    }

    public async Task OnBattleEndedAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        var replayId = GetReplayId(context.RoomId);
        lock (_lock)
        {
            if (!_isSaving)
            {
                return;
            }

            ExpireUploadRequests();
            _uploadRequests[replayId] = _clockService.CurrentUtcDateTimeOffset;
        }

        _bot.Say(context.RoomId, "/savereplay");
        await WriteAsync($"{replayId}.log", string.Join('\n', context.Lines) + '\n', cancellationToken);
    }

    public async Task OnReplayUploadedAsync(string url, CancellationToken cancellationToken = default)
    {
        string replayId;
        lock (_lock)
        {
            replayId = _uploadRequests.Keys.FirstOrDefault(id => IsReplayOf(url, id));
            if (replayId == null)
            {
                return;
            }

            _uploadRequests.Remove(replayId);
            _downloadCount++;
        }

        var isSaved = await DownloadAsync(url, replayId, cancellationToken);
        lock (_lock)
        {
            _downloadCount--;
            if (isSaved)
            {
                _savedCount++;
                _lastSaved = replayId;
            }
            else
            {
                _failedCount++;
            }
        }
    }

    /// <summary>
    /// Showdown's replay id for a battle room (rooms.ts getReplayData): the room id without "battle-", and without
    /// the password of a hidden battle ("-{password}pw").
    /// </summary>
    public static string GetReplayId(string roomId)
    {
        var id = roomId.StartsWith(BATTLE_ROOM_PREFIX, StringComparison.Ordinal)
            ? roomId[BATTLE_ROOM_PREFIX.Length..]
            : roomId;
        return WithoutPassword(id);
    }

    private static string WithoutPassword(string id)
    {
        var lastHyphen = id.LastIndexOf('-');
        return id.EndsWith(HIDDEN_ROOM_SUFFIX, StringComparison.Ordinal) && lastHyphen > 0 ? id[..lastHyphen] : id;
    }

    /// <summary>
    /// The uploaded id may also start with the server's id ("{serverid}-{id}") and end with a password.
    /// </summary>
    private static bool IsReplayOf(string url, string replayId)
    {
        var uploadedId = WithoutPassword(url[(url.LastIndexOf('/') + 1)..]);
        return uploadedId == replayId || uploadedId.EndsWith($"-{replayId}", StringComparison.Ordinal);
    }

    private async Task<bool> DownloadAsync(string url, string replayId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MAX_DOWNLOAD_ATTEMPTS; attempt++)
        {
            try
            {
                var response = await _httpService.SendForStringAsync(HttpRequest.Get($"{url}.json"),
                    cancellationToken);
                var isWritten = await WriteAsync($"{replayId}.json", response.Data, cancellationToken);
                if (isWritten)
                {
                    Log.Information("Saved the replay {ReplayId} to {Directory}", replayId, _directory);
                }

                return isWritten;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not download the replay {Url} (attempt {Attempt} of {MaxAttempts})",
                    url, attempt, MAX_DOWNLOAD_ATTEMPTS);
            }

            if (attempt < MAX_DOWNLOAD_ATTEMPTS)
            {
                await _systemService.SleepAsync(DOWNLOAD_RETRY_DELAY, cancellationToken);
            }
        }

        return false;
    }

    private async Task<bool> WriteAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllTextAsync(Path.Join(_directory, fileName), content, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Could not write {File} to {Directory}", fileName, _directory);
            return false;
        }
    }

    private void ExpireUploadRequests()
    {
        var now = _clockService.CurrentUtcDateTimeOffset;
        var expired = _uploadRequests
            .Where(request => now - request.Value >= UPLOAD_TIMEOUT)
            .Select(request => request.Key)
            .ToList();
        foreach (var replayId in expired)
        {
            _uploadRequests.Remove(replayId);
            _failedCount++;
            Log.Warning("Showdown did not upload the replay of {ReplayId}", replayId);
        }
    }
}
