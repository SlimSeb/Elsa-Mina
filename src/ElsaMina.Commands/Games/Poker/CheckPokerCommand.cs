using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokercheck", Aliases = ["pk"])]
public class CheckPokerCommand : PokerActionCommandBase
{
    public CheckPokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override Task ExecuteAsync(IContext context, IPokerGame game, string argument) =>
        game.CheckAsync(context.Sender);
}
