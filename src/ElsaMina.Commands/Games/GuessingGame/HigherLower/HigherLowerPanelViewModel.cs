using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame.HigherLower;

public class HigherLowerPanelViewModel : LocalizableViewModel
{
    public string CategoryLabelKey { get; set; }
    public Pokemon PokemonA { get; set; }
    public Pokemon PokemonB { get; set; }
    public string ValueADisplay { get; set; }
    public string ValueBDisplay { get; set; }
    // When the round is over, reveal Pokémon B's value and highlight the comparison
    public bool IsRevealed { get; set; }
    // Whether Pokémon B's value is higher than Pokémon A's
    public bool IsHigher { get; set; }
    public IReadOnlyDictionary<GuessingGamePlayer, int> Scores { get; set; }
    public int CurrentTurn { get; set; }
    public int TurnsCount { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }
}
