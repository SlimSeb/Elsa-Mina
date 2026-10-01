using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Lifecycle;

public class BotLifecycleService : IBotLifecycleService
{
    private readonly IEnumerable<IBotLifecycleParticipant> _participants;

    public BotLifecycleService(IEnumerable<IBotLifecycleParticipant> participants)
    {
        _participants = participants;
    }

    public Task OnStartingAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_participants.Select(participant => participant.OnStartingAsync(cancellationToken)));
    }

    public Task OnExitingAsync(CancellationToken cancellationToken = default)
    {
        // A participant failing to flush must not stop the others from flushing.
        return Task.WhenAll(_participants.Select(participant => ExitSafelyAsync(participant, cancellationToken)));
    }

    private static async Task ExitSafelyAsync(IBotLifecycleParticipant participant, CancellationToken cancellationToken)
    {
        try
        {
            await participant.OnExitingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Warning("{0} did not finish its shutdown work in time", participant.GetType().Name);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "{0} failed during shutdown", participant.GetType().Name);
        }
    }
}
