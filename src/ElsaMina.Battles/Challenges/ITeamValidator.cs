namespace ElsaMina.Battles.Challenges;

/// <summary>
/// Asks the server whether a team is valid in a format (/vtm).
/// </summary>
public interface ITeamValidator
{
    /// <summary>
    /// True when the server says the team is valid, false when it rejects it, null without an answer (no reply in
    /// time, rate limited, unknown format).
    /// </summary>
    Task<bool?> ValidateAsync(string packedTeam, string format, CancellationToken cancellationToken = default);

    /// <summary>
    /// A popup from the server: the answer to the pending validation, if it is one.
    /// </summary>
    void OnPopup(string popup);
}
