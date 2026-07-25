using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Calls the running game off.
/// </summary>
[NamedCommand("presidentend")]
public class EndPresidentCommand : EndGameCommand<IPresidentGame>
{
    protected override string ResourcePrefix => "president";
}
