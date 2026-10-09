using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public class AcceptChallengeHandler : Handler
{
    private const string CHALLENGE_MESSAGE_PREFIX = "/challenge ";
    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "pm" };
    
    private readonly IBot _bot;

    public AcceptChallengeHandler(IBot bot)
    {
        _bot = bot;
    }

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null, CancellationToken cancellationToken = default)
    {
        if (parts.Length < 5 || parts[1] != "pm" || !parts[4].StartsWith(CHALLENGE_MESSAGE_PREFIX))
        {
            return Task.CompletedTask;
        }

        var challenger = parts[2][1..];
        var format = parts[4][CHALLENGE_MESSAGE_PREFIX.Length..];
        if (format.ToLowerAlphaNum().Contains("randombattle"))
        {
            _bot.Send("|/utm null");
            _bot.Send($"|/accept {challenger}");

        }

        return Task.CompletedTask;
    }
}