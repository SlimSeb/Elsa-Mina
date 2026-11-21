namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// Joins tournaments in the formats the bot plays and gets its matches started.
/// </summary>
public interface ITournamentPlayingService
{
    /// <summary>
    /// A |tournament| message of <paramref name="roomId"/>, or its |init| or |deinit| (the bot joined or left it).
    /// </summary>
    Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Joins the tournament in its signup phase in <paramref name="roomId"/>, when it is in a format the bot plays.
    /// </summary>
    TournamentJoinResult Join(string roomId);

    /// <summary>
    /// Leaves the tournament of <paramref name="roomId"/> (forfeiting it once started). False when the bot is not in it.
    /// </summary>
    bool Leave(string roomId);
}
