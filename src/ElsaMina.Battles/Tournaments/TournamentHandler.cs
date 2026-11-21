using ElsaMina.Core.Handlers;

namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// A room's tournament messages, and the bot joining (|init|) or leaving (|deinit|) the room.
/// </summary>
public class TournamentHandler : Handler
{
    private readonly ITournamentPlayingService _tournamentPlayingService;

    public TournamentHandler(ITournamentPlayingService tournamentPlayingService)
    {
        _tournamentPlayingService = tournamentPlayingService;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } =
        new HashSet<string> { "tournament", "init", "deinit" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        return _tournamentPlayingService.HandleMessageAsync(parts, roomId, cancellationToken);
    }
}
