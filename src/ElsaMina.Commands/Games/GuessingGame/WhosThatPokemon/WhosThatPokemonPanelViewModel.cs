using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;

public class WhosThatPokemonPanelViewModel : LocalizableViewModel
{
    public Pokemon Pokemon { get; set; }
    public bool IsRevealed { get; set; }
    public IReadOnlyDictionary<GuessingGamePlayer, int> Scores { get; set; }
    public int CurrentTurn { get; set; }
    public int TurnsCount { get; set; }
    public TimeSpan RemainingTime { get; set; }
}
