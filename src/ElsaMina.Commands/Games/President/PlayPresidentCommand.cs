using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Plays one or more cards of the same rank onto the pile, e.g. <c>-prp 7</c> for a single seven or
/// <c>-prp k 2</c> for a pair of kings. Without an explicit count, the pile's required count is used.
/// </summary>
[NamedCommand("presidentplay", Aliases = ["prp"])]
public class PlayPresidentCommand : PresidentActionCommandBase
{
    public PlayPresidentCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, IPresidentGame game, string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            context.ReplyLocalizedMessage("president_card_unknown", argument);
            return;
        }

        var rank = PresidentCard.ParseRank(parts[0]);
        if (rank is null)
        {
            context.ReplyLocalizedMessage("president_card_unknown", parts[0]);
            return;
        }

        var count = 0;
        if (parts.Length > 1
            && (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                || count is < 1 or > 4))
        {
            context.ReplyLocalizedMessage("president_card_unknown", argument);
            return;
        }

        await game.PlayAsync(context.Sender, rank.Value, count);
    }
}
