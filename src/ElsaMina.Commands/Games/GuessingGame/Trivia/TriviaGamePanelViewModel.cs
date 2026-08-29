using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class TriviaGamePanelViewModel : LocalizableViewModel
{
    public string Question { get; set; }
    public string Category { get; set; }
    public string Difficulty { get; set; }
    public TriviaQuestionType Type { get; set; }
    public IReadOnlyList<string> Options { get; set; } = [];
    public string CorrectAnswer { get; set; }
    public bool ShowCorrectAnswer { get; set; }
    public IReadOnlyDictionary<GuessingGamePlayer, int> Scores { get; set; }
    public int CurrentTurn { get; set; }
    public int TurnsCount { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }

    public bool IsOptionCorrect(int optionIndex)
    {
        if (Options == null || optionIndex < 0 || optionIndex >= Options.Count)
        {
            return false;
        }

        return string.Equals(Options[optionIndex], CorrectAnswer, StringComparison.OrdinalIgnoreCase);
    }
}
