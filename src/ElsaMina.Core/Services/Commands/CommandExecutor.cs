using System.Collections.Concurrent;
using System.Diagnostics;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Commands;

public class CommandExecutor : ICommandExecutor, IBotLifecycleParticipant
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly IEnumerable<IDynamicCommandProvider> _dynamicCommandProviders;
    private readonly ITelemetryService _telemetryService;
    private readonly IFeatureSwitchService _featureSwitchService;

    private readonly ConcurrentDictionary<Guid, RunningCommand> _runningCommands = new();

    public CommandExecutor(
        ICommandRegistry commandRegistry,
        IEnumerable<IDynamicCommandProvider> dynamicCommandProviders,
        ITelemetryService telemetryService,
        IFeatureSwitchService featureSwitchService)
    {
        _commandRegistry = commandRegistry;
        _dynamicCommandProviders = dynamicCommandProviders;
        _telemetryService = telemetryService;
        _featureSwitchService = featureSwitchService;
    }

    #region Public API

    public IEnumerable<ICommand> GetAllCommands()
    {
        return _commandRegistry.Commands.DistinctBy(command => command.Name);
    }

    public async Task TryExecuteCommandAsync(
        string commandName,
        IContext context,
        CancellationToken cancellationToken = default)
    {
        if (_featureSwitchService.IsMaydayActive && !context.IsSenderWhitelisted)
        {
            return;
        }

        var command = _commandRegistry.Find(commandName);
        if (command == null)
        {
            // Les commandes custom et les suggestions peuvent taper la bdd : on les lance en fond comme les commandes
            _ = Track(commandName, context, cancellationToken,
                token => TryExecuteFallbackAsync(commandName, context, token));
            return;
        }

        if (!CanCommandBeRan(context, command))
        {
            return;
        }

        Log.Information("Executing {0} as a normal command", commandName);
        var execution = Track(command.Name, context, cancellationToken,
            token => RunCommandAsync(command, context, token));

        if (command.RunsInMessageOrder)
        {
            await execution;
        }
    }

    public Task WhenAllCommandsCompletedAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_runningCommands.Values.Select(running => running.Task)).WaitAsync(cancellationToken);
    }

    public Task OnExitingAsync(CancellationToken cancellationToken)
    {
        return WhenAllCommandsCompletedAsync(cancellationToken);
    }

    #endregion

    #region Execution tracking

    private Task Track(string commandName, IContext context, CancellationToken externalToken,
        Func<CancellationToken, Task> run)
    {
        var executionId = Guid.NewGuid();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = Task.Run(async () =>
        {
            // On attend l'enregistrement juste en dessous, comme ça la commande est toujours retirée après avoir été ajoutée
            await started.Task;
            try
            {
                await run(linkedCts.Token);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Command {0} ({1}) crashed with context : {2}", commandName, executionId,
                    context);
                await ReportErrorAsync(context, exception);
            }
            finally
            {
                _runningCommands.TryRemove(executionId, out _);
                linkedCts.Dispose();
            }
        }, CancellationToken.None);

        _runningCommands[executionId] = new RunningCommand(executionId, commandName, context, linkedCts, task);
        started.SetResult();
        return task;
    }

    private async Task RunCommandAsync(ICommand command, IContext context, CancellationToken cancellationToken)
    {
        using var activity = _telemetryService.StartActivity("command.execute");
        activity?.SetTag("command.name", command.Name);
        activity?.SetTag("room", context.RoomId);
        activity?.SetTag("sender", context.Sender?.UserId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await command.RunAsync(context, cancellationToken);
            _telemetryService.RecordCommandExecuted(command.Name, "ok");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _telemetryService.RecordCommandExecuted(command.Name, "cancelled");
            Log.Information("Command {0} was cancelled", command.Name);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            _telemetryService.RecordCommandError(command.Name);
            throw;
        }
        finally
        {
            _telemetryService.RecordCommandDuration(stopwatch.Elapsed.TotalMilliseconds, command.Name);
        }
    }

    private static async Task ReportErrorAsync(IContext context, Exception exception)
    {
        try
        {
            await context.HandleErrorAsync(exception);
        }
        catch (Exception reportException)
        {
            Log.Error(reportException, "Could not report command error to the user");
        }
    }

    public bool TryCancel(Guid executionId)
    {
        if (!_runningCommands.TryGetValue(executionId, out var running))
        {
            return false;
        }

        running.CancellationTokenSource.Cancel();
        return true;
    }

    public IEnumerable<RunningCommand> RunningCommands => _runningCommands.Values;

    #endregion

    #region Dynamic commands, auto-correct & guards

    private async Task TryExecuteFallbackAsync(string commandName, IContext context,
        CancellationToken cancellationToken)
    {
        foreach (var provider in _dynamicCommandProviders)
        {
            if (await provider.TryExecuteAsync(commandName, context, cancellationToken))
            {
                return;
            }
        }

        Log.Error("Could not find command {0}", commandName);

        var canRunAutoCorrect =
            context.IsPrivateMessage ||
            (await context.Room
                .GetParameterValueAsync(Parameter.HasCommandAutoCorrect, cancellationToken))
            .ToBoolean();

        if (canRunAutoCorrect)
        {
            ReplyWithAutoCorrect(commandName, context);
        }
    }

    private void ReplyWithAutoCorrect(string commandName, IContext context)
    {
        var maxLevenshteinDistance = commandName.Length switch
        {
            <= 6 => 1,
            <= 12 => 2,
            _ => 3
        };

        var closestCommands = GetAllCommands()
            .Where(command => !command.IsHidden)
            .SelectMany(command => (string[])[..command.Aliases, command.Name])
            .Where(possible => possible.LevenshteinDistance(commandName) <= maxLevenshteinDistance)
            .ToArray();

        if (closestCommands.Length == 0)
        {
            return;
        }

        context.ReplyLocalizedMessage(
            "command_autocorrect_suggestion",
            commandName,
            string.Join(", ", closestCommands));
    }

    private bool CanCommandBeRan(IContext context, ICommand command)
    {
        if (command.IsPrivateMessageOnly && !context.IsPrivateMessage)
        {
            return false;
        }

        if (context.IsPrivateMessage &&
            !(command.IsAllowedInPrivateMessage || command.IsPrivateMessageOnly))
        {
            return false;
        }

        if (command.IsWhitelistOnly && !context.IsSenderWhitelisted)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(command.Category)
            && !_featureSwitchService.IsFeatureEnabled(command.Category)
            && !context.IsSenderWhitelisted)
        {
            return false;
        }

        if (!context.HasRankOrHigher(command.RequiredRank))
        {
            return false;
        }

        if (!context.IsPrivateMessage && command.RoomRestriction.Any() &&
            !command.RoomRestriction.Contains(context.RoomId))
        {
            return false;
        }

        return true;
    }

    #endregion
}
