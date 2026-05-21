using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.RockPaperScissors;

public abstract class PlayRpsCommandBase : Command
{
    private readonly IRoomsManager _roomsManager;

    protected PlayRpsCommandBase(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    protected abstract RpsChoice Choice { get; }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim().ToLowerAlphaNum();
        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IRpsGame rpsGame)
        {
            await rpsGame.Play(context.Sender.Name.ToLowerAlphaNum(), Choice);
        }
    }
}
