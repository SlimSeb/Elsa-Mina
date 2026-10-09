using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Lifecycle;

public class BotLifecycleService : IBotLifecycleService
{
    private readonly Lazy<IEnumerable<IBotLifecycleParticipant>> _participants;

    // Lazy : plein de participants dépendent du bot, qui dépend de ce service => on les construit seulement au
    // démarrage du bot, une fois qu'il existe
    public BotLifecycleService(Lazy<IEnumerable<IBotLifecycleParticipant>> participants)
    {
        _participants = participants;
    }

    public Task OnStartingAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_participants.Value.Select(participant => participant.OnStartingAsync(cancellationToken)));
    }

    public Task OnExitingAsync(CancellationToken cancellationToken = default)
    {
        // Un participant qui plante pendant son flush doit pas empêcher les autres de flush
        return Task.WhenAll(_participants.Value.Select(participant => ExitSafelyAsync(participant, cancellationToken)));
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
