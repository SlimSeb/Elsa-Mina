using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotmisere", Aliases = ["tarotmisery"])]
public class DeclareMisereTarotCommand : GameActionCommandBase<ITarotGame>
{
    public DeclareMisereTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        await game.DeclareMisereAsync(context.Sender);
    }
}
