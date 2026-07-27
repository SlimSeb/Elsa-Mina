using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Lets a player in the running game ask to be replaced by a substitute. Running it again cancels the
/// pending request. Works both in the room and from a panel button (a private message whose target is
/// the room id).
/// </summary>
[NamedCommand("belotesub", Aliases = ["bsub"])]
public class RequestBeloteSubCommand : RequestSubGameCommand<IBeloteGame>
{
    public RequestBeloteSubCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override string ResourcePrefix => "belote";
}
