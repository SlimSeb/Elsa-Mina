using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Services.Commands;

/// <summary>
/// Handles command names that are not registered commands, such as custom commands stored per room.
/// Providers are tried in registration order; the first one returning true wins.
/// </summary>
public interface IDynamicCommandProvider
{
    Task<bool> TryExecuteAsync(string commandName, IContext context, CancellationToken cancellationToken = default);
}
