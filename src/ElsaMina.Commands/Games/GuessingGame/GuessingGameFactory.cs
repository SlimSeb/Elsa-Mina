namespace ElsaMina.Commands.Games.GuessingGame;

public class GuessingGameFactory : IGuessingGameFactory
{
    private readonly Dictionary<string, Func<GuessingGame>> _factoriesByCommand;

    public GuessingGameFactory(IEnumerable<GuessingGameRegistration> registrations)
    {
        _factoriesByCommand = registrations.ToDictionary(
            registration => registration.CommandName,
            registration => registration.CreateGame);
    }

    public GuessingGame Create(string commandName)
    {
        return commandName != null && _factoriesByCommand.TryGetValue(commandName, out var factory)
            ? factory()
            : null;
    }
}
