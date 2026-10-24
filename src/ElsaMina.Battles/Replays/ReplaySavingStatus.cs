namespace ElsaMina.Battles.Replays;

/// <summary>
/// What <see cref="IReplaySavingService"/> has done since the bot started.
/// </summary>
/// <param name="IsSaving">Whether the battles that end are saved.</param>
/// <param name="Directory">Where they are saved.</param>
/// <param name="SavedCount">Replays downloaded.</param>
/// <param name="FailedCount">Replays Showdown did not upload, or that could not be downloaded or written.</param>
/// <param name="WaitingCount">Replays asked for and not downloaded yet.</param>
/// <param name="LastSaved">The id of the last replay downloaded, null before the first one.</param>
public sealed record ReplaySavingStatus(
    bool IsSaving,
    string Directory,
    int SavedCount,
    int FailedCount,
    int WaitingCount,
    string LastSaved);
