using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public abstract class CommandMessageHandler : MessageHandler
{
    private readonly IRoomsManager _roomsManager;
    private readonly IConfiguration _configuration;
    private readonly ICommandExecutor _commandExecutor;
    private readonly string _botUserId;
    
    protected CommandMessageHandler(IContextFactory contextFactory,
        IRoomsManager roomsManager,
        IConfiguration configuration,
        ICommandExecutor commandExecutor) : base(contextFactory)
    {
        _roomsManager = roomsManager;
        _configuration = configuration;
        _commandExecutor = commandExecutor;
        _botUserId = configuration.Name.ToLowerAlphaNum();
    }

    public override async Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.RoomId == null || !_roomsManager.HasRoom(context.RoomId))
        {
            return;
        }
        if (_configuration.RoomBlacklist.Contains(context.RoomId))
        {
            return;
        }

        if (context.Command == null)
        {
            return;
        }

        if (context.Sender.UserId == _botUserId)
        {
            return;
        }
        try
        {
            await _commandExecutor.TryExecuteCommandAsync(context.Command, context, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Command execution crashed with context : {0}", context);
            await context.HandleErrorAsync(exception, cancellationToken);
        }
    }
}
