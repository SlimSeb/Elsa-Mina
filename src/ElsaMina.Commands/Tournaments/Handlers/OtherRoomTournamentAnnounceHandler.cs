using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Formats;

namespace ElsaMina.Commands.Tournaments.Handlers;

public class OtherRoomTournamentAnnounceHandler : Handler
{
    private readonly IFormatsManager _formatsManager;
    private readonly IEventAnnouncer _eventAnnouncer;

    public OtherRoomTournamentAnnounceHandler(IFormatsManager formatsManager, IEventAnnouncer eventAnnouncer)
    {
        _formatsManager = formatsManager;
        _eventAnnouncer = eventAnnouncer;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "tournament" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 4 || parts[1] != "tournament" || parts[2] != "create")
        {
            return Task.CompletedTask;
        }

        var format = _formatsManager.GetCleanFormat(parts[3]);
        return _eventAnnouncer.AnnounceToLinkedRoomsAsync(roomId, EventAnnounceType.Tournament, "tour_announce_message",
            [format, roomId], cancellationToken);
    }
}
