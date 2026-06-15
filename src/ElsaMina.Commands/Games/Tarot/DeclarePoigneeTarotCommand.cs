using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotpoignee", Aliases = ["tarothandful"])]
public class DeclarePoigneeTarotCommand : TarotActionCommandBase
{
    public DeclarePoigneeTarotCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, ITarotGame game, string argument)
    {
        await game.DeclarePoigneeAsync(context.Sender);
    }
}
