using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Closes the lobby and deals the first hand.
/// </summary>
[NamedCommand("belotestart", Aliases = ["belotebegin"])]
public class BeginBeloteCommand : BeginGameCommand<IBeloteGame>
{
    protected override string ResourcePrefix => "belote";
}
