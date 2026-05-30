using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotbid", Aliases = ["tb"])]
public class BidTarotCommand : TarotActionCommandBase
{
    public BidTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        var bid = ParseBid(argument);
        if (bid is null)
        {
            context.ReplyLocalizedMessage("tarot_bid_unknown");
            return;
        }

        await game.BidAsync(context.Sender, bid.Value);
    }

    private static TarotBid? ParseBid(string argument) => argument.Trim().ToLowerInvariant().Replace(" ", "") switch
    {
        "pass" or "p" or "passe" => TarotBid.Pass,
        "petite" or "prise" or "petit" => TarotBid.Petite,
        "garde" or "g" => TarotBid.Garde,
        "gardesans" or "gs" or "gardesanslechien" => TarotBid.GardeSans,
        "gardecontre" or "gc" or "gardecontrelechien" => TarotBid.GardeContre,
        _ => null
    };
}
