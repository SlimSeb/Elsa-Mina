using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Takes a seat in the game running in the room.
/// </summary>
[NamedCommand("belotejoin")]
public class JoinBeloteCommand : JoinGameCommand<IBeloteGame>
{
    protected override string ResourcePrefix => "belote";
}
