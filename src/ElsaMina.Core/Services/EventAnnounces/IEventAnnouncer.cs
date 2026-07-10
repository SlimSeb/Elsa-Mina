namespace ElsaMina.Core.Services.EventAnnounces;

public interface IEventAnnouncer
{
    /// <summary>
    /// Broadcasts a localized <c>/wall</c> announcement to every room linked to <paramref name="sourceRoomId"/>
    /// through the <c>EventAnnounces</c> configuration. Each receiving room gets the message rendered in its own
    /// culture, falling back to the default locale when the room is unknown. A receiving room only gets the
    /// message if its <c>EventAnnouncesType</c> parameter allows announcements of <paramref name="announceType"/>.
    /// </summary>
    /// <param name="sourceRoomId">The room where the event happened (an <c>EventAnnounces</c> broadcasting room).</param>
    /// <param name="announceType">The kind of event being announced, matched against each room's filter.</param>
    /// <param name="resourceKey">The resource key of the message to render for each receiving room.</param>
    /// <param name="formatArguments">The arguments used to format the localized message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task AnnounceToLinkedRoomsAsync(string sourceRoomId, EventAnnounceType announceType, string resourceKey,
        object[] formatArguments, CancellationToken cancellationToken = default);
}
