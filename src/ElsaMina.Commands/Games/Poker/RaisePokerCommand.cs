using System.Globalization;
using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerraise", Aliases = ["pr", "pokerbet"])]
public class RaisePokerCommand : GameActionCommandBase<IPokerGame>
{
    public RaisePokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    // The poker panel buttons send the room id on its own, with no argument after it.
    protected override bool RequiresArgument => false;

    protected override async Task ExecuteAsync(IContext context, IPokerGame game, string argument)
    {
        if (!long.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amountTo)
            || amountTo <= 0)
        {
            context.ReplyLocalizedMessage("poker_raise_invalid_amount");
            return;
        }

        await game.RaiseAsync(context.Sender, amountTo);
    }
}
