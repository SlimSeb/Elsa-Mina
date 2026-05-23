namespace ElsaMina.Core.Handlers;

public abstract class Handler : IHandler
{
    public bool IsEnabled { get; set; } = true;
    public virtual string Identifier => GetType().FullName;
    public virtual IReadOnlySet<string> HandledMessageTypes => null;

    public abstract Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default);
}