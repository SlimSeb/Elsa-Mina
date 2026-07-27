using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Takes a seat in the game running in the room.
/// </summary>
[NamedCommand("tarotjoin", Aliases = ["tj"])]
public class JoinTarotCommand : JoinGameCommand<ITarotGame>
{
    protected override string ResourcePrefix => "tarot";
}
