using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Closes the lobby and deals the first hand.
/// </summary>
[NamedCommand("tarotstart", Aliases = ["tarotbegin"])]
public class BeginTarotCommand : BeginGameCommand<ITarotGame>
{
    protected override string ResourcePrefix => "tarot";
}
