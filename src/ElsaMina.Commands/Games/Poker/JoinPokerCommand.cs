using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// Takes a seat in the game running in the room.
/// </summary>
[NamedCommand("pokerjoin", Aliases = ["pj"])]
public class JoinPokerCommand : JoinGameCommand<IPokerGame>
{
    protected override string ResourcePrefix => "poker";
}
