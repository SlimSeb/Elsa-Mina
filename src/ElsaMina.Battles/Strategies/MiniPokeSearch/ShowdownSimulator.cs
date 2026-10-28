using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// A few Node processes running search_sim.js (copied next to the bot from tools/minipoke) on the configured
/// pokemon-showdown package, one JSON request per line on stdin, one reply per line on stdout. They start on the first
/// request; one that fails or takes too long is killed and replaced. After a few failures in a row the simulator turns
/// itself off until the bot restarts (the models then play without search).
/// </summary>
public sealed class ShowdownSimulator : IShowdownSimulator, IDisposable
{
    private const int WORKERS = 2;
    private const string DEFAULT_NODE = "node";
    private const int MAX_FAILURES_IN_A_ROW = 5;
    private static readonly TimeSpan REPLY_TIMEOUT = TimeSpan.FromSeconds(10);

    private readonly string _nodePath;
    private readonly string _showdownDirectory;
    private readonly string _scriptPath;
    private readonly Channel<Process> _idle = Channel.CreateUnbounded<Process>();
    private readonly List<Process> _processes = [];
    private readonly Lock _lock = new();
    private int _started;
    private bool _disposed;
    private volatile bool _failed;
    private int _failuresInARow;

    public ShowdownSimulator(IBattlesConfiguration configuration)
    {
        _nodePath = string.IsNullOrWhiteSpace(configuration.NodePath) ? DEFAULT_NODE : configuration.NodePath;
        _showdownDirectory = configuration.ShowdownDirectory ?? "";
        _scriptPath = Path.Join(AppContext.BaseDirectory, "Search", "search_sim.js");
    }

    public bool IsAvailable => !_failed && !string.IsNullOrWhiteSpace(_showdownDirectory) &&
                               Directory.Exists(Path.Join(_showdownDirectory, "dist", "sim")) &&
                               File.Exists(_scriptPath);

    public async Task<SimulationReply> SimulateAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        StartWorkers();
        var process = await _idle.Reader.ReadAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(REPLY_TIMEOUT);
            await process.StandardInput.WriteLineAsync(request.ToJsonString().AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);
            var line = await process.StandardOutput.ReadLineAsync(timeout.Token)
                       ?? throw new InvalidOperationException("The simulator closed its output");
            var reply = JsonSerializer.Deserialize<SimulationReply>(line)
                        ?? throw new InvalidOperationException("Empty simulator reply");
            if (reply.Error != null)
            {
                throw new InvalidOperationException($"Simulator error: {reply.Error}");
            }

            _idle.Writer.TryWrite(process);
            Interlocked.Exchange(ref _failuresInARow, 0);
            return reply;
        }
        catch
        {
            // Its output may be out of step with its input now: replace it
            Replace(process);
            if (Interlocked.Increment(ref _failuresInARow) >= MAX_FAILURES_IN_A_ROW && !_failed)
            {
                _failed = true;
                Log.Warning("The Showdown simulator failed {Count} times in a row; minipoke plays without search",
                    MAX_FAILURES_IN_A_ROW);
            }

            throw;
        }
    }

    private void StartWorkers()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            for (var i = 0; i < WORKERS; i++)
            {
                _idle.Writer.TryWrite(Start());
            }
        }
        catch (Exception exception)
        {
            // Node is missing or broken: searching stays off until the bot restarts
            _failed = true;
            Log.Warning(exception, "Could not start the Showdown simulator ({Node}); minipoke plays without search",
                _nodePath);
            throw;
        }
    }

    private Process Start()
    {
        var startInfo = new ProcessStartInfo(_nodePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(_scriptPath);
        startInfo.ArgumentList.Add(_showdownDirectory);
        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException($"Could not start {_nodePath}");
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                Log.Warning("Showdown simulator: {Line}", args.Data);
            }
        };
        process.BeginErrorReadLine();
        lock (_lock)
        {
            _processes.Add(process);
        }

        return process;
    }

    private void Replace(Process process)
    {
        Kill(process);
        lock (_lock)
        {
            _processes.Remove(process);
            if (_disposed)
            {
                return;
            }
        }

        try
        {
            _idle.Writer.TryWrite(Start());
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not restart the Showdown simulator");
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone
        }

        process.Dispose();
    }

    public void Dispose()
    {
        List<Process> processes;
        lock (_lock)
        {
            _disposed = true;
            processes = [.._processes];
            _processes.Clear();
        }

        _idle.Writer.TryComplete();
        foreach (var process in processes)
        {
            Kill(process);
        }
    }
}
