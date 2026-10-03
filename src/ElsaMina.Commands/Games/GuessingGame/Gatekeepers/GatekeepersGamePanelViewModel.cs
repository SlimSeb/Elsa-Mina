using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame.Gatekeepers;

public class GatekeepersGamePanelViewModel : LocalizableViewModel
{
    public string FootprintSprite { get; set; }
    public IReadOnlyCollection<Pokemon> CurrentOptions { get; set; } = [];
    public bool ShowSilhouettes { get; set; }
    public bool ShowPortraits { get; set; }
    public IReadOnlyDictionary<GuessingGamePlayer, int> Scores { get; set; }
    public int CurrentTurn { get; set; }
    public int TurnsCount { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }
    // When the round has been won, highlight the correct answer in green
    public bool ShouldShowCorrectAnswer { get; set; }
    public Pokemon CurrentValidAnswer { get; set; }
}