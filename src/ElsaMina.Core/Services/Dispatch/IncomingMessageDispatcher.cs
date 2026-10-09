using ElsaMina.Core.Services.Scheduling;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Dispatch;

public class IncomingMessageDispatcher : IIncomingMessageDispatcher
{
    // Max de frames reçues mais pas encore traitées. Au-delà on arrête de lire la socket,
    // comme ça une room bloquée peut pas faire exploser la mémoire
    private const int MAX_PENDING_FRAMES = 4096;

    private readonly IBot _bot;
    private readonly IKeyedTaskQueue _roomQueue;
    private readonly SemaphoreSlim _pendingFrames = new(MAX_PENDING_FRAMES, MAX_PENDING_FRAMES);

    public IncomingMessageDispatcher(IBot bot, IKeyedTaskQueue roomQueue)
    {
        _bot = bot;
        _roomQueue = roomQueue;
    }

    public async Task DispatchAsync(string frame, CancellationToken cancellationToken = default)
    {
        await _pendingFrames.WaitAsync(cancellationToken);
        var roomId = ShowdownFrame.GetRoomId(frame);
        _ = _roomQueue.EnqueueAsync(roomId, () => HandleFrameAsync(frame, roomId));
    }

    public async Task WhenIdleAsync(CancellationToken cancellationToken = default)
    {
        while (_pendingFrames.CurrentCount < MAX_PENDING_FRAMES)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    private async Task HandleFrameAsync(string frame, string roomId)
    {
        try
        {
            await _bot.HandleReceivedMessageAsync(frame);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while handling message in room {0}", roomId);
        }
        finally
        {
            _pendingFrames.Release();
        }
    }
}
