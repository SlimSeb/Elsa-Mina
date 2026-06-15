using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotchelem", Aliases = ["tarotslam"])]
public class AnnounceSlamTarotCommand : TarotActionCommandBase
{
    public AnnounceSlamTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        await game.AnnounceSlamAsync(context.Sender);
    }
}
