using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Development.LagTest;

public class LagTestHandler : Handler
{
    private readonly IConfiguration _configuration;
    private readonly ILagTestManager _lagTestManager;

    public LagTestHandler(IConfiguration configuration, ILagTestManager lagTestManager)
    {
        _configuration = configuration;
        _lagTestManager = lagTestManager;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "c:" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 5 || parts[1] != "c:")
        {
            return Task.CompletedTask;
        }

        var senderId = parts[3].ToLowerAlphaNum();
        var botId = _configuration.Name.ToLowerAlphaNum();
        if (senderId != botId)
        {
            return Task.CompletedTask;
        }

        if (parts[4] != LagTestManager.LAG_TEST_MARKER)
        {
            return Task.CompletedTask;
        }

        _lagTestManager.HandleEcho(roomId);
        return Task.CompletedTask;
    }
}
