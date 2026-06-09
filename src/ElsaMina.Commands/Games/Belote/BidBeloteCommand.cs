using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("belotebid", Aliases = ["bb"])]
public class BidBeloteCommand : BeloteActionCommandBase
{
    public BidBeloteCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, IBeloteGame game, string argument)
    {
        var normalized = argument.Trim().ToLowerInvariant().Replace(" ", string.Empty);

        switch (normalized)
        {
            case "pass" or "p" or "passe":
                await game.BidAsync(context.Sender, pass: true, null);
                return;
            case "take" or "t" or "prendre" or "prend" or "prends":
                await game.BidAsync(context.Sender, pass: false, null);
                return;
        }

        var suit = BeloteCard.ParseSuit(normalized);
        if (suit is null)
        {
            context.ReplyLocalizedMessage("belote_bid_unknown");
            return;
        }

        await game.BidAsync(context.Sender, pass: false, suit);
    }
}
