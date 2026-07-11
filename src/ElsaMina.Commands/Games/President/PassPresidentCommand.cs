using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("presidentpass", Aliases = ["prpass"])]
public class PassPresidentCommand : PresidentActionCommandBase
{
    public PassPresidentCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override async Task ExecuteAsync(IContext context, IPresidentGame game, string argument)
    {
        await game.PassAsync(context.Sender);
    }
}
