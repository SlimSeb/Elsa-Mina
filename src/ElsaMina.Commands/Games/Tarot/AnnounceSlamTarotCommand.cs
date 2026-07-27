using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotchelem", Aliases = ["tarotslam"])]
public class AnnounceSlamTarotCommand : GameActionCommandBase<ITarotGame>
{
    public AnnounceSlamTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        await game.AnnounceSlamAsync(context.Sender);
    }
}
