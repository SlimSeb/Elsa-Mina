using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public abstract class ChatMessageHandler : MessageHandler
{
    protected ChatMessageHandler(IContextFactory contextFactory) : base(contextFactory)
    {
    }

    protected override ContextType HandledContextType => ContextType.Room;

    public override IReadOnlySet<string> HandledMessageTypes => new HashSet<string>() { "c:" };
}