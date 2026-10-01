namespace ElsaMina.Core.Services.Scheduling;

public class KeyedTaskQueue : IKeyedTaskQueue
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task> _tails = new();

    public int ActiveKeyCount
    {
        get
        {
            lock (_lock)
            {
                return _tails.Count;
            }
        }
    }

    public Task EnqueueAsync(string key, Func<Task> work)
    {
        key ??= string.Empty;
        Task next;
        lock (_lock)
        {
            var previous = _tails.GetValueOrDefault(key, Task.CompletedTask);
            next = RunAfterAsync(previous, work);
            _tails[key] = next;
        }

        _ = RemoveTailWhenCompletedAsync(key, next);
        return next;
    }

    private static async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // The previous item's failure belongs to its own caller, it must not stop this one.
        }

        // Always leave the caller's stack first, so work never runs inside EnqueueAsync's lock.
        await Task.Yield();
        await work().ConfigureAwait(false);
    }

    private async Task RemoveTailWhenCompletedAsync(string key, Task tail)
    {
        try
        {
            await tail.ConfigureAwait(false);
        }
        catch
        {
            // Observed by the caller of EnqueueAsync.
        }

        lock (_lock)
        {
            if (_tails.TryGetValue(key, out var current) && current == tail)
            {
                _tails.Remove(key);
            }
        }
    }
}
