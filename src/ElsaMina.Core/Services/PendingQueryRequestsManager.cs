using System.Collections.Concurrent;
using ElsaMina.Core.Services.System;

namespace ElsaMina.Core.Services;

public class PendingQueryRequestsManager<TKey, TResult>
    where TKey : notnull
{
    private readonly ISystemService _systemService;
    private readonly TimeSpan _cancelDelay;
    private readonly Func<TResult> _timeoutResultFactory;

    private readonly ConcurrentDictionary<TKey, (TaskCompletionSource<TResult> Tcs, CancellationTokenSource TimeoutCts)>
        _pendingRequests = new();

    public PendingQueryRequestsManager(ISystemService systemService, TimeSpan cancelDelay, Func<TResult> timeoutResultFactory)
    {
        _systemService = systemService;
        _cancelDelay = cancelDelay;
        _timeoutResultFactory = timeoutResultFactory;
    }

    public Task<TResult> AddOrReplace(TKey requestKey, CancellationToken cancellationToken = default)
    {
        if (_pendingRequests.TryRemove(requestKey, out var oldEntry))
        {
            oldEntry.TimeoutCts.Cancel();
            oldEntry.Tcs.TrySetCanceled();
        }

        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _pendingRequests[requestKey] = (tcs, timeoutCts);

        _ = RunTimeoutAsync(requestKey, timeoutCts.Token);

        return tcs.Task;
    }

    public bool TryResolve(TKey requestKey, TResult result)
    {
        if (!_pendingRequests.TryRemove(requestKey, out var entry))
        {
            return false;
        }

        entry.TimeoutCts.Cancel();
        entry.Tcs.TrySetResult(result);
        return true;
    }

    public bool TryResolveOnlyPending(TResult result)
    {
        if (_pendingRequests.Count != 1)
        {
            return false;
        }

        return _pendingRequests
            .Select(request => TryResolve(request.Key, result))
            .First();
    }

    private async Task RunTimeoutAsync(TKey requestKey, CancellationToken cancellationToken)
    {
        try
        {
            await _systemService.SleepAsync(_cancelDelay, cancellationToken);
            if (_pendingRequests.TryRemove(requestKey, out var entry))
            {
                entry.Tcs.TrySetResult(_timeoutResultFactory());
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: cancelled because data arrived or request was replaced
        }
    }
}
