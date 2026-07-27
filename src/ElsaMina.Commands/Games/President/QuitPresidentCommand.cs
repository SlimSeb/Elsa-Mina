using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.President;

/// <summary>
/// Gives a seat back while the game is still gathering players.
/// </summary>
[NamedCommand("presidentquit", Aliases = ["presidentleave", "prq"])]
public class QuitPresidentCommand : QuitGameCommand<IPresidentGame>
{
    protected override string ResourcePrefix => "president";
}
