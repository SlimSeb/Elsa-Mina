using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("rock")]
public class RockRpsCommand : PlayRpsCommandBase
{
    public RockRpsCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override RpsChoice Choice => RpsChoice.Rock;
}
