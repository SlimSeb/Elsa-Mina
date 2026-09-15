namespace ElsaMina.Core.Handlers;

public abstract class Handler : IHandler
{
    private static readonly IReadOnlySet<string> NO_MESSAGE_TYPE_FILTER = new HashSet<string>();

    public bool IsEnabled { get; set; } = true;
    public virtual string Identifier => GetType().FullName;
    public virtual IReadOnlySet<string> HandledMessageTypes => NO_MESSAGE_TYPE_FILTER;

    public abstract Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default);
}