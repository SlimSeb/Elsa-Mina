using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Gives a seat back while the game is still gathering players.
/// </summary>
[NamedCommand("tarotquit", Aliases = ["tarotleave", "tq"])]
public class QuitTarotCommand : QuitGameCommand<ITarotGame>
{
    protected override string ResourcePrefix => "tarot";
}
