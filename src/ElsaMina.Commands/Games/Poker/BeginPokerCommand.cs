using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// Closes the lobby and deals the first hand.
/// </summary>
[NamedCommand("pokerstart", Aliases = ["pokerbegin"])]
public class BeginPokerCommand : BeginGameCommand<IPokerGame>
{
    protected override string ResourcePrefix => "poker";
}
