namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// What the server said about a room's tournament, from the bot's point of view. Read and changed under
/// <see cref="TournamentPlayingService"/>'s lock.
/// </summary>
public class TournamentState
{
    /// <summary>
    /// The format the battles are played in ("gen9randombattle"), without its custom rules.
    /// </summary>
    public string FormatId { get; set; }

    public bool IsStarted { get; set; }
    public bool IsJoined { get; set; }

    /// <summary>
    /// The bot asked to join this tournament: it does not again on its own, even after leaving it.
    /// </summary>
    public bool HasRequestedJoin { get; set; }

    /// <summary>
    /// The opponent of the bot's next match while the bot is not ready for it ("(next available player)" when not
    /// known yet). On servers older than readying, the player who challenged the bot.
    /// </summary>
    public string Challenged { get; set; }

    /// <summary>
    /// The opponent of the bot's next match once the bot is ready for it. On servers older than readying, the player
    /// the bot challenged.
    /// </summary>
    public string Challenging { get; set; }

    /// <summary>
    /// On servers older than readying, the players the bot can challenge. Always empty on newer ones.
    /// </summary>
    public IReadOnlyList<string> Challenges { get; set; } = [];

    /// <summary>
    /// The last match command sent (/tour acceptchallenge, /tour challenge USER), until the server answers it.
    /// </summary>
    public string SentAction { get; set; }

    /// <summary>
    /// How many times <see cref="SentAction"/> was sent without an answer.
    /// </summary>
    public int UnansweredCount { get; set; }

    /// <summary>
    /// Sends <see cref="SentAction"/> again when the server does not answer it.
    /// </summary>
    public CancellationTokenSource Retry { get; set; }
}
