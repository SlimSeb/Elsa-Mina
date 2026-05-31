using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerraise", Aliases = ["pr", "pokerbet"])]
public class RaisePokerCommand : PokerActionCommandBase
{
    public RaisePokerCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

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
