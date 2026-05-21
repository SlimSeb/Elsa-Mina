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
    private readonly ConcurrentDictionary<string, Logger> _roomLoggers = new();

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
        var logger = _roomLoggers.GetOrAdd(roomId, CreateRoomLogger);
        var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
        logger.Information("{Line}", $"[{utcTime:HH:mm:ss} UTC] {username}: {message}");
    }

    private static Logger CreateRoomLogger(string roomId)
    {
        return new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(LOGS_DIRECTORY, roomId, "chatlog.txt"),
                rollingInterval: RollingInterval.Day,
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
        foreach (var roomId in _roomLoggers.Keys)
        {
            try
            {
                await FlushRoomAsync(roomId, cancellationToken);
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

    private async Task FlushRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        // Serilog names rolling files: {baseName}{yyyyMMdd}.{ext}
        var localPath = Path.Combine(LOGS_DIRECTORY, roomId, $"chatlog{DateTime.Now:yyyyMMdd}.txt");
        if (!File.Exists(localPath))
        {
            return;
        }

        await using var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var s3Key = $"chatlogs/{roomId}/{DateTime.UtcNow:yyyy-MM-dd}.txt";
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
        foreach (var logger in _roomLoggers.Values)
        {
            logger.Dispose();
        }

        _disposed = true;
    }
}
