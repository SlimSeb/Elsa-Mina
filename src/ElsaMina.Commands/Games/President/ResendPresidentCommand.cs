using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Sends the player their private hand page again.
/// </summary>
[NamedCommand("presidentresend", Aliases = ["presidentpage", "prr"])]
public class ResendPresidentCommand : ResendGameCommand<IPresidentGame>
{
    protected override string ResourcePrefix => "president";
}
