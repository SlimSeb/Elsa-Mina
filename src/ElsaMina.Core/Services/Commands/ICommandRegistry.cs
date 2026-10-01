namespace ElsaMina.Core.Services.Commands;

public interface ICommandRegistry
{
    /// <summary>
    /// Every registered command, once each.
    /// </summary>
    IReadOnlyCollection<ICommand> Commands { get; }

    /// <summary>
    /// Finds a command by its name or one of its aliases, or returns null.
    /// </summary>
    ICommand Find(string nameOrAlias);
}
