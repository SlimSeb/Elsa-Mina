namespace ElsaMina.Core.Utils;

public sealed class PeriodicTimerRunner : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Func<Task> _onTick;
    private readonly bool _runOnce;
    private readonly Lock _stateLock = new();

    private CancellationTokenSource _cts;
    private Task _runTask;

    public PeriodicTimerRunner(TimeSpan interval, Func<Task> onTick, bool runOnce = false)
    {
        _interval = interval;
        _onTick = onTick;
        _runOnce = runOnce;
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_runTask != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _runTask = RunAsync(_cts);
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        CancellationTokenSource cts;
        lock (_stateLock)
        {
            if (_cts == null)
            {
                return;
            }

            cts = _cts;
            _cts = null;
            _runTask = null;
        }

        cts.Cancel();
    }

    private async Task RunAsync(CancellationTokenSource cts)
    {
        try
        {
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                await _onTick();
                if (_runOnce)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: cancelled on Stop()
        }
        catch
        {
            // ignored
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                    _runTask = null;
                }
            }

            cts.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
