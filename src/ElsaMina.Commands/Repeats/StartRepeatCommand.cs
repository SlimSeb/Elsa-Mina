using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Repeats;

[NamedCommand("startrepeat")]
public class StartRepeatCommand : Command
{
    private readonly IRepeatsManager _repeatsManager;

    public StartRepeatCommand(IRepeatsManager repeatsManager)
    {
        _repeatsManager = repeatsManager;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    // Format : room, message, délai
    public override string GetTargetRoomId(IContext context) => context.GetRoomIdArgument(0);

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        string message;
        uint delayInMinutes;
        try
        {
            var parts = context.Target.Split(',');
            roomId = GetTargetRoomId(context) ?? throw new FormatException("The room is missing");
            message = parts[1].Trim();
            delayInMinutes = uint.Parse(parts[2]);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not parse repeat arguments");
            context.ReplyLocalizedMessage("repeat_start_error");
            return;
        }
        
        await _repeatsManager.StartRepeatAsync(roomId, message, TimeSpan.FromMinutes(delayInMinutes),
            cancellationToken);
        context.ReplyLocalizedMessage("repeat_start_success");
    }
}