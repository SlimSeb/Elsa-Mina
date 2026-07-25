using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Calls the running game off.
/// </summary>
[NamedCommand("beloteend")]
public class EndBeloteCommand : EndGameCommand<IBeloteGame>
{
    protected override string ResourcePrefix => "belote";
}
