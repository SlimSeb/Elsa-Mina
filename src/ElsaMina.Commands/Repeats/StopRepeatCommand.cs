using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Repeats;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Repeats;

[NamedCommand("stop-repeat", Aliases = ["end-repeat", "cancel-repeat", "stoprepeat", "endrepeat"])]
public class StopRepeatCommand : Command
{
    private readonly IRepeatsManager _repeatsManager;

    public StopRepeatCommand(IRepeatsManager repeatsManager)
    {
        _repeatsManager = repeatsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(context.Target, out var guid))
        {
            context.ReplyLocalizedMessage("repeat_stop_invalid_id");
            return;
        }
        
        var repeat = _repeatsManager.GetRepeat(guid);
        if (repeat == null)
        {
            context.ReplyLocalizedMessage("repeat_stop_doesntexist");
            return;
        }
        
        if (!await context.HasSufficientRankInRoom(repeat.RoomId, Rank.Driver, cancellationToken))
        {
            context.ReplyLocalizedMessage("repeat_stop_rights");
            return;
        }

        var ended = await _repeatsManager.StopRepeatAsync(guid, cancellationToken);
        if (!ended)
        {
            context.ReplyLocalizedMessage("repeat_stop_error");
            return;
        }

        context.ReplyLocalizedMessage("repeat_stop_success");
    }
}