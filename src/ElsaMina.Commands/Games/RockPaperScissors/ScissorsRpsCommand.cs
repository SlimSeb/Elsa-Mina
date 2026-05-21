using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("scissors")]
public class ScissorsRpsCommand : PlayRpsCommandBase
{
    public ScissorsRpsCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override RpsChoice Choice => RpsChoice.Scissors;
}
