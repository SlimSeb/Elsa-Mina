using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Services.Commands;

public interface ICommandExecutor
{
    IEnumerable<RunningCommand> RunningCommands { get; }
    IEnumerable<ICommand> GetAllCommands();

    /// <summary>
    /// Runs the command named <paramref name="commandName"/>, a custom command, or replies with suggestions.
    /// Completes once the command no longer holds up the room's message order: when it has finished for a
    /// command that <see cref="ICommand.RunsInMessageOrder"/>, as soon as it has started otherwise.
    /// Failures are reported to the user through the context, never thrown.
    /// </summary>
    Task TryExecuteCommandAsync(string commandName, IContext context, CancellationToken cancellationToken = default);

    bool TryCancel(Guid executionId);

    /// <summary>
    /// Completes once every command started so far has finished.
    /// </summary>
    Task WhenAllCommandsCompletedAsync(CancellationToken cancellationToken = default);
}
