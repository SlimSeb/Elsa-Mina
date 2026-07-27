using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Closes the lobby and deals the first hand.
/// </summary>
[NamedCommand("presidentstart", Aliases = ["presidentbegin"])]
public class BeginPresidentCommand : BeginGameCommand<IPresidentGame>
{
    protected override string ResourcePrefix => "president";
}
