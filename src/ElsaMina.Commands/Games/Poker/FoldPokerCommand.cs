using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerfold", Aliases = ["pf"])]
public class FoldPokerCommand : PokerActionCommandBase
{
    public FoldPokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override Task ExecuteAsync(IContext context, IPokerGame game, string argument) =>
        game.FoldAsync(context.Sender);
}
