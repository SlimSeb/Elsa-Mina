using ElsaMina.Core.Contexts;

namespace ElsaMina.Core.Services.Commands;

/// <summary>
/// Gère les noms de commandes qui sont pas des commandes enregistrées, genre les commandes custom par room.
/// Les providers sont testés dans l'ordre d'enregistrement, le premier qui renvoie true gagne
/// </summary>
public interface IDynamicCommandProvider
{
    Task<bool> TryExecuteAsync(string commandName, IContext context, CancellationToken cancellationToken = default);
}
