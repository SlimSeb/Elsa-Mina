using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public sealed class ChatMessageCommandHandler : CommandMessageHandler
{
    public ChatMessageCommandHandler(IContextFactory contextFactory,
        IRoomsManager roomsManager,
        IConfiguration configuration,
        ICommandExecutor commandExecutor)
        : base(contextFactory, roomsManager, configuration, commandExecutor)
    {
    }

    public override IReadOnlySet<string> HandledMessageTypes => new HashSet<string> { "c:" };

    protected override ContextType HandledContextType => ContextType.Room;
}