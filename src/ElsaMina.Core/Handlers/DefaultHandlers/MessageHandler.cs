using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public abstract class MessageHandler : Handler
{
    private readonly IContextFactory _contextFactory;

    protected MessageHandler(IContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    protected abstract ContextType HandledContextType { get; }

    public sealed override async Task HandleReceivedMessageAsync(string[] parts, string roomId = null, CancellationToken cancellationToken = default)
    {
        var context = _contextFactory.TryBuildContextFromReceivedMessage(parts, roomId);
        if (context == null || HandledContextType != context.Type)
        {
            return;
        }
        
        await HandleMessageAsync(context, cancellationToken);
    }

    public abstract Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default);
}