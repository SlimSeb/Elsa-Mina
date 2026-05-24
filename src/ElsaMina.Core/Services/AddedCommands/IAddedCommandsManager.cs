using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Core.Services.AddedCommands;

public interface IAddedCommandsManager
{
    Task<bool> TryExecuteAddedCommand(string commandName, IContext context, CancellationToken cancellationToken);
    Task ExecuteAddedCommand(AddedCommand command, IContext context, CancellationToken cancellationToken);
}