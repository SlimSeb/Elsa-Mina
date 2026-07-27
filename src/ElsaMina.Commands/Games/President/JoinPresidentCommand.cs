using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Takes a seat in the game running in the room.
/// </summary>
[NamedCommand("presidentjoin", Aliases = ["prj"])]
public class JoinPresidentCommand : JoinGameCommand<IPresidentGame>
{
    protected override string ResourcePrefix => "president";
}
