using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotdiscard", Aliases = ["tarotecart"])]
public class DiscardTarotCommand : TarotActionCommandBase
{
    public DiscardTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        var tokens = argument.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cards = new List<TarotCard>(tokens.Length);
        foreach (var token in tokens)
        {
            var card = TarotCard.Parse(token);
            if (card is null)
            {
                context.ReplyLocalizedMessage("tarot_card_unknown", token);
                return;
            }

            cards.Add(card);
        }

        await game.DiscardAsync(context.Sender, cards);
    }
}
