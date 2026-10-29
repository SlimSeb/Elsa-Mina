namespace ElsaMina.Battles.Challenges;

/// <summary>
/// Answers the challenges users send the bot.
/// </summary>
public interface IChallengeService
{
    /// <summary>
    /// Accepts <paramref name="challenger"/>'s challenge in <paramref name="format"/> with a team for it, or rejects it
    /// when there is none. Never throws: it runs in the background.
    /// </summary>
    Task HandleChallengeAsync(string challenger, string format, CancellationToken cancellationToken = default);
}
