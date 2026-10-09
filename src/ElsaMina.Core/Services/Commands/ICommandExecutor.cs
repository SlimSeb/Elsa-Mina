using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Services.Commands;

public interface ICommandExecutor
{
    IEnumerable<RunningCommand> RunningCommands { get; }
    IEnumerable<ICommand> GetAllCommands();

    /// <summary>
    /// Lance la commande <paramref name="commandName"/>, une commande custom, ou répond avec des suggestions.
    /// Se termine quand la commande bloque plus l'ordre des messages de la room : quand elle est finie pour une
    /// commande qui <see cref="ICommand.RunsInMessageOrder"/>, dès qu'elle a démarré sinon.
    /// Les erreurs sont remontées à l'user via le contexte, jamais throw.
    /// </summary>
    Task TryExecuteCommandAsync(string commandName, IContext context, CancellationToken cancellationToken = default);

    bool TryCancel(Guid executionId);

    /// <summary>
    /// Se termine quand toutes les commandes lancées jusqu'ici sont finies
    /// </summary>
    Task WhenAllCommandsCompletedAsync(CancellationToken cancellationToken = default);
}
