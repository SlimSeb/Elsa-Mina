namespace ElsaMina.Commands.Games.GuessingGame;

public interface IGuessingGameFactory
{
    /// <summary>
    /// Creates the guessing game started by <paramref name="commandName"/>, or returns null for an unknown name.
    /// </summary>
    GuessingGame Create(string commandName);
}
