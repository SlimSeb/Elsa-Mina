using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public abstract class PrivateMessageHandler : MessageHandler
{
    protected PrivateMessageHandler(IContextFactory contextFactory) : base(contextFactory)
    {
    }

    public override IReadOnlySet<string> HandledMessageTypes => new HashSet<string> { "pm" };
    protected override ContextType HandledContextType => ContextType.Pm;
}