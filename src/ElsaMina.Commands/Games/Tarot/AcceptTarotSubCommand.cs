using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Lets a user who is not in the game take the seat of a player who asked for a substitute. Triggered
/// from the room sub panel button (<c>tarotsubaccept playerid</c>) or in a private message whose target
/// is prefixed with the room id (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("tarotsubaccept", Aliases = ["tsa"])]
public class AcceptTarotSubCommand : AcceptSubGameCommand<ITarotGame>
{
    public AcceptTarotSubCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override string ResourcePrefix => "tarot";
}
