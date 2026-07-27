using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerfold", Aliases = ["pf"])]
public class FoldPokerCommand : GameActionCommandBase<IPokerGame>
{
    public FoldPokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    // The poker panel buttons send the room id on its own, with no argument after it.
    protected override bool RequiresArgument => false;

    protected override Task ExecuteAsync(IContext context, IPokerGame game, string argument) =>
        game.FoldAsync(context.Sender);
}
