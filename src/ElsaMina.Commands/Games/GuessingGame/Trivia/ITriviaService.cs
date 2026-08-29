namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public interface ITriviaService
{
    Task<IReadOnlyList<TriviaQuestion>> GetQuestionsAsync(int amount, CancellationToken cancellationToken = default);
}
