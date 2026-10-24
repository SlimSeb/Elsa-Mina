namespace ElsaMina.Battles.Replays;

/// <summary>
/// Keeps the bot's battles on disk while -savereplays is on.
/// </summary>
public interface IReplaySavingService
{
    ReplaySavingStatus Status { get; }
    void Start();
    void Stop();

    /// <summary>
    /// Writes what the bot received in the battle room and asks Showdown to upload the replay. Call it before
    /// leaving the room.
    /// </summary>
    Task OnBattleEndedAsync(BattleContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Showdown uploaded a replay at <paramref name="url"/>: downloads it when the bot asked for it. Takes as long
    /// as the download (retries included) and never throws.
    /// </summary>
    Task OnReplayUploadedAsync(string url, CancellationToken cancellationToken = default);
}
