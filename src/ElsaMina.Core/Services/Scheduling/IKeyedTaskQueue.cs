namespace ElsaMina.Core.Services.Scheduling;

/// <summary>
/// Runs work items one after the other for a given key, while work for different keys runs concurrently.
/// </summary>
public interface IKeyedTaskQueue
{
    /// <summary>
    /// Queues <paramref name="work"/> behind the work already queued for <paramref name="key"/>.
    /// The returned task completes when <paramref name="work"/> has completed, and carries its exception if it fails.
    /// A failing work item never prevents the next ones for the same key from running.
    /// </summary>
    Task EnqueueAsync(string key, Func<Task> work);

    /// <summary>
    /// The number of keys that currently have queued or running work.
    /// </summary>
    int ActiveKeyCount { get; }
}
