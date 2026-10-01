namespace ElsaMina.Core.Handlers;

public interface IHandler
{
    bool IsEnabled { get; set; }
    string Identifier { get; }

    /// <summary>
    /// The message types this handler handles.
    /// Return an empty set (or null) to handle every message type.
    /// </summary>
    IReadOnlySet<string> HandledMessageTypes { get; }

    Task HandleReceivedMessageAsync(string[] parts, string roomId = null, CancellationToken cancellationToken = default);
}