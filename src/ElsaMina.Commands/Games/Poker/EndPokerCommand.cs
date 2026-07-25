using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// Calls the running game off.
/// </summary>
[NamedCommand("pokerend")]
public class EndPokerCommand : EndGameCommand<IPokerGame>
{
    protected override string ResourcePrefix => "poker";
}
