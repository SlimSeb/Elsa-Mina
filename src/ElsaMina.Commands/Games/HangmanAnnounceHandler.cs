using System.Text.RegularExpressions;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.EventAnnounces;

namespace ElsaMina.Commands.Games;

public partial class HangmanAnnounceHandler : Handler
{
    [GeneratedRegex(@"hangman(\d+)")]
    private static partial Regex HangmanIdRegex();

    private readonly IEventAnnouncer _eventAnnouncer;

    public override IReadOnlySet<string> HandledMessageTypes => (HashSet<string>)["uhtml"];

    private uint _lastId;

    public HangmanAnnounceHandler(IEventAnnouncer eventAnnouncer)
    {
        _eventAnnouncer = eventAnnouncer;
    }

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts[1] != "uhtml")
        {
            return Task.CompletedTask;
        }

        var match = HangmanIdRegex().Match(parts[2]);
        if (!match.Success)
        {
            return Task.CompletedTask;
        }

        if (!uint.TryParse(match.Groups[1].Value, out var hangmanId))
        {
            return Task.CompletedTask;
        }

        if (hangmanId <= _lastId)
        {
            return Task.CompletedTask;
        }

        _lastId = hangmanId;

        return _eventAnnouncer.AnnounceToLinkedRoomsAsync(roomId, EventAnnounceType.Game, "hangman_started_in",
            [roomId], cancellationToken);
    }
}
