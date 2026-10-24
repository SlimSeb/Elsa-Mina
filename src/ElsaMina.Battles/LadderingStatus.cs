namespace ElsaMina.Battles;

/// <summary>
/// What <see cref="ILadderingService"/> is doing.
/// </summary>
/// <param name="Format">The format laddered, null when not laddering.</param>
/// <param name="Since">When laddering in that format started.</param>
/// <param name="IsSearching">The server's last updatesearch says the bot is searching for a battle.</param>
/// <param name="IsInGame">The server's last updatesearch says the bot is playing a battle.</param>
/// <param name="IsWaitingForConfirmation">A search was sent and the server has not accepted it yet.</param>
/// <param name="NextSearchAt">When the next search is sent, while one waits for Showdown's rate limit or a retry.</param>
/// <param name="FailedSearchCount">Searches in a row the server did not accept.</param>
public sealed record LadderingStatus(
    string Format,
    DateTimeOffset? Since,
    bool IsSearching,
    bool IsInGame,
    bool IsWaitingForConfirmation,
    DateTimeOffset? NextSearchAt,
    int FailedSearchCount)
{
    public bool IsLaddering => Format != null;
}
