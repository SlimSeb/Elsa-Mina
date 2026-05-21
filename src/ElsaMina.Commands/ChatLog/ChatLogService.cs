using System.Collections.Concurrent;
using ElsaMina.FileSharing;
using Serilog;
using Serilog.Core;
using AppLog = ElsaMina.Logging.Log;

namespace ElsaMina.Commands.ChatLog;

public class ChatLogService : IChatLogService
{
    private static readonly TimeSpan FLUSH_INTERVAL = TimeSpan.FromMinutes(30);
    private const string LOGS_DIRECTORY = "chatlogs";

    private readonly IFileSharingService _fileSharingService;
    private readonly TimeSpan _flushInterval;
    private readonly ConcurrentDictionary<string, (DateOnly Date, Logger Logger)> _roomLoggers = new();
    private readonly Lock _loggerCreationLock = new();

    private CancellationTokenSource _cts;
    private bool _disposed;

    public ChatLogService(IFileSharingService fileSharingService)
        : this(fileSharingService, FLUSH_INTERVAL)
    {
    }

    public ChatLogService(IFileSharingService fileSharingService, TimeSpan flushInterval)
    {
        _fileSharingService = fileSharingService;
        _flushInterval = flushInterval;
    }

    public void Append(string roomId, long unixTimestamp, string username, string message)
    {
        var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
        var date = DateOnly.FromDateTime(utcTime);
        var logger = GetOrCreateLogger(roomId, date);
        logger.Information("{Line}", $"[{utcTime:HH:mm:ss} UTC] {username}: {message}");
    }

    private Logger GetOrCreateLogger(string roomId, DateOnly date)
    {
        if (_roomLoggers.TryGetValue(roomId, out var existing) && existing.Date == date)
        {
            return existing.Logger;
        }

        lock (_loggerCreationLock)
        {
            if (_roomLoggers.TryGetValue(roomId, out existing) && existing.Date == date)
            {
                return existing.Logger;
            }

            var newLogger = CreateRoomLogger(roomId, date);
            if (_roomLoggers.TryGetValue(roomId, out var stale))
            {
                stale.Logger.Dispose();
            }

            _roomLoggers[roomId] = (date, newLogger);
            return newLogger;
        }
    }

    private static Logger CreateRoomLogger(string roomId, DateOnly date)
    {
        return new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(LOGS_DIRECTORY, roomId, $"chatlog{date:yyyyMMdd}.txt"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();
    }

    public void Start()
    {
        AppLog.Information("ChatLogService starting with flush interval {FlushInterval}", _flushInterval);
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => FlushLoopAsync(_cts.Token));
    }

    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_flushInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushAllAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            AppLog.Information("ChatLogService flush loop cancelled");
        }
    }

    private async Task FlushAllAsync(CancellationToken cancellationToken)
    {
        foreach (var (roomId, (date, _)) in _roomLoggers)
        {
            try
            {
                await FlushRoomAsync(roomId, date, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "ChatLogService failed to flush room {RoomId}", roomId);
            }
        }
    }

    private async Task FlushRoomAsync(string roomId, DateOnly date, CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(LOGS_DIRECTORY, roomId, $"chatlog{date:yyyyMMdd}.txt");
        if (!File.Exists(localPath))
        {
            return;
        }

        await using var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var s3Key = $"chatlogs/{roomId}/{date:yyyy-MM-dd}.txt";
        await _fileSharingService.CreateFileAsync(fileStream, s3Key, null, "text/plain", cancellationToken);
        AppLog.Information("ChatLogService uploaded {S3Key}", s3Key);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        foreach (var (_, logger) in _roomLoggers.Values)
        {
            logger.Dispose();
        }

        _disposed = true;
    }
}
