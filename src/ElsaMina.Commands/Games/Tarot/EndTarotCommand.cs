using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Calls the running game off.
/// </summary>
[NamedCommand("tarotend")]
public class EndTarotCommand : EndGameCommand<ITarotGame>
{
    protected override string ResourcePrefix => "tarot";
}
