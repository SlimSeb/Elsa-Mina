using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Selects the card(s) the president (or vice-president) gives back during the exchange, e.g.
/// <c>-prg 3h</c> to toggle a card or <c>-prg 3h 4c</c> to hand over both at once.
/// </summary>
[NamedCommand("presidentgive", Aliases = ["prg"])]
public class GivePresidentCommand : GameActionCommandBase<IPresidentGame>
{
    public GivePresidentCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, IPresidentGame game, string argument)
    {
        var tokens = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cards = new List<PresidentCard>();
        foreach (var token in tokens)
        {
            var card = PresidentCard.Parse(token);
            if (card is null)
            {
                context.ReplyLocalizedMessage("president_card_unknown", token);
                return;
            }

            cards.Add(card);
        }

        await game.GiveAsync(context.Sender, cards);
    }
}
