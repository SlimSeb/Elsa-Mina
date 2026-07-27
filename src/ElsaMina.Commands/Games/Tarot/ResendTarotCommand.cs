using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Sends the player their private hand page again.
/// </summary>
[NamedCommand("tarotresend", Aliases = ["tarotpage", "tr"])]
public class ResendTarotCommand : ResendGameCommand<ITarotGame>
{
    protected override string ResourcePrefix => "tarot";
}
