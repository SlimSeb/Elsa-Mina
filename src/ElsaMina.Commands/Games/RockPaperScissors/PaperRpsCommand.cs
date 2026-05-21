using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("paper")]
public class PaperRpsCommand : PlayRpsCommandBase
{
    public PaperRpsCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override RpsChoice Choice => RpsChoice.Paper;
}
