using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Sends the player their private hand page again.
/// </summary>
[NamedCommand("beloteresend", Aliases = ["belotepage", "br"])]
public class ResendBeloteCommand : ResendGameCommand<IBeloteGame>
{
    protected override string ResourcePrefix => "belote";
}
