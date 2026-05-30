using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotcall", Aliases = ["tarotking"])]
public class CallKingTarotCommand : TarotActionCommandBase
{
    public CallKingTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        var card = TarotCard.Parse(argument);
        if (card is null)
        {
            context.ReplyLocalizedMessage("tarot_card_unknown", argument);
            return;
        }

        await game.CallKingAsync(context.Sender, card);
    }
}
