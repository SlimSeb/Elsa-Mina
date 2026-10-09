namespace ElsaMina.Core.Services.Scheduling;

/// <summary>
/// Exécute les tâches l'une après l'autre pour une même clé, et en parallèle pour des clés différentes
/// </summary>
public interface IKeyedTaskQueue
{
    /// <summary>
    /// Met <paramref name="work"/> en file derrière ce qui attend déjà pour <paramref name="key"/>.
    /// La tâche renvoyée se termine quand <paramref name="work"/> est fini, et porte son exception s'il plante.
    /// Une tâche qui plante empêche jamais les suivantes de la même clé de tourner.
    /// </summary>
    Task EnqueueAsync(string key, Func<Task> work);

    /// <summary>
    /// Le nb de clés qui ont du taf en file ou en cours
    /// </summary>
    int ActiveKeyCount { get; }
}
