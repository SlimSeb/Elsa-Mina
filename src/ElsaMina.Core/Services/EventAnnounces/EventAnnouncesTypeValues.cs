namespace ElsaMina.Core.Services.EventAnnounces;

/// <summary>
/// Stored values of the <c>EventAnnouncesType</c> room parameter, which controls the kinds of cross-room
/// announcements a room is willing to receive.
/// </summary>
public static class EventAnnouncesTypeValues
{
    public const string All = "all";
    public const string TournamentsOnly = "tournaments";
    public const string GamesOnly = "games";
    public const string None = "none";

    /// <summary>
    /// Returns whether a room configured with <paramref name="storedValue"/> should receive an announcement
    /// of the given <paramref name="announceType"/>. Unknown or missing values fall back to allowing everything,
    /// preserving the default behaviour.
    /// </summary>
    public static bool Allows(string storedValue, EventAnnounceType announceType) => storedValue switch
    {
        TournamentsOnly => announceType == EventAnnounceType.Tournament,
        GamesOnly => announceType == EventAnnounceType.Game,
        None => false,
        _ => true
    };
}
