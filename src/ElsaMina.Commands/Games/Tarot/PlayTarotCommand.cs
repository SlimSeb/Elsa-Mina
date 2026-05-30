using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotplay", Aliases = ["tp"])]
public class PlayTarotCommand : TarotActionCommandBase
{
    public PlayTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
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

        await game.PlayAsync(context.Sender, card);
    }
}
