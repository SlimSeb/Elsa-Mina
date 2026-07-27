using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Lets a user who is not in the game take the seat of a player who asked for a substitute. Triggered
/// from the room sub panel button (<c>presidentsubaccept playerid</c>) or in a private message whose target
/// is prefixed with the room id (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("presidentsubaccept", Aliases = ["prsa"])]
public class AcceptPresidentSubCommand : AcceptSubGameCommand<IPresidentGame>
{
    public AcceptPresidentSubCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override string ResourcePrefix => "president";
}
