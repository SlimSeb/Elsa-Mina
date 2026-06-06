using ElsaMina.Logging;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public class ErrorHandler : Handler
{
    public override IReadOnlySet<string> HandledMessageTypes => new HashSet<string> { "error", "popup" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        switch (parts.Length)
        {
            case >= 3 when parts[1] == "error":
                Log.Error("Received error message from server : {Error}", parts[2]);
                break;
            case > 2 when parts[1] == "popup":
                Log.Information("Received popup message from server : {Popup}", string.Join("|", parts[2..]));
                break;
        }

        return Task.CompletedTask;
    }
}