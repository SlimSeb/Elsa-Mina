using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("beloteplay", Aliases = ["bp"])]
public class PlayBeloteCommand : BeloteActionCommandBase
{
    public PlayBeloteCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, IBeloteGame game, string argument)
    {
        var card = BeloteCard.Parse(argument);
        if (card is null)
        {
            context.ReplyLocalizedMessage("belote_card_unknown", argument);
            return;
        }

        await game.PlayAsync(context.Sender, card);
    }
}
