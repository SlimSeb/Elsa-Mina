using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Lets a user who is not in the game take the seat of a player who asked for a substitute. Triggered
/// from the room sub panel button (<c>belotesubaccept playerid</c>) or in a private message whose target
/// is prefixed with the room id (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("belotesubaccept", Aliases = ["bsa"])]
public class AcceptBeloteSubCommand : AcceptSubGameCommand<IBeloteGame>
{
    public AcceptBeloteSubCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override string ResourcePrefix => "belote";
}
