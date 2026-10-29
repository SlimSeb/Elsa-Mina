using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles.Challenges;

/// <summary>
/// A user challenging the bot: "|pm| User| Bot|/challenge gen9ou|...". The challenge is answered in the background:
/// finding a team waits for the server's popups, which arrive on the same lane as this PM.
/// </summary>
public class ChallengeHandler : Handler
{
    private const string CHALLENGE_PREFIX = "/challenge ";

    private readonly IChallengeService _challengeService;
    private readonly IConfiguration _configuration;

    public ChallengeHandler(IChallengeService challengeService, IConfiguration configuration)
    {
        _challengeService = challengeService;
        _configuration = configuration;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "pm" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 5 || parts[1] != "pm" || !parts[4].StartsWith(CHALLENGE_PREFIX, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // The sender and receiver come with their rank symbol (" User", "+User")
        var challenger = parts[2][1..].ToLowerAlphaNum();
        var receiver = parts[3][1..].ToLowerAlphaNum();
        var bot = _configuration.Name.ToLowerAlphaNum();
        var format = parts[4][CHALLENGE_PREFIX.Length..].Trim();
        if (challenger == bot || receiver != bot || format.Length == 0)
        {
            return Task.CompletedTask;
        }

        _ = _challengeService.HandleChallengeAsync(challenger, format, CancellationToken.None);
        return Task.CompletedTask;
    }
}
