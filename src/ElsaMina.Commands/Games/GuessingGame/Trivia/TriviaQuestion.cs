namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class TriviaQuestion
{
    public string Question { get; init; }
    public string Category { get; init; }
    public string Difficulty { get; init; }
    public TriviaQuestionType Type { get; init; }
    public string CorrectAnswer { get; init; }
    public IReadOnlyList<string> Options { get; init; }
    public IReadOnlyList<string> ValidAnswers { get; init; }
}
