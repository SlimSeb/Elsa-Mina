using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokercall", Aliases = ["pl"])]
public class CallPokerCommand : PokerActionCommandBase
{
    public CallPokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override Task ExecuteAsync(IContext context, IPokerGame game, string argument) =>
        game.CallAsync(context.Sender);
}
