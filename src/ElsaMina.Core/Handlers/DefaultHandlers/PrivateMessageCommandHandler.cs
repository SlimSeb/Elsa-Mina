using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public sealed class PrivateMessageCommandHandler : CommandMessageHandler
{
    public PrivateMessageCommandHandler(IContextFactory contextFactory,
        IRoomsManager roomsManager,
        IConfiguration configuration,
        ICommandExecutor commandExecutor) : base(contextFactory, roomsManager, configuration, commandExecutor)
    {
    }

    public override IReadOnlySet<string> HandledMessageTypes => new HashSet<string> { "pm" };
    protected override ContextType HandledContextType => ContextType.Pm;
}