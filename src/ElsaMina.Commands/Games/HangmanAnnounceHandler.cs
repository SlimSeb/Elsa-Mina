using System.Globalization;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games;

public class HangmanAnnounceHandler : Handler
{
    private static readonly Regex HANGMAN_ID_REGEX = new(@"hangman(\d+)");

    private readonly IConfiguration _configuration;
    private readonly IBot _bot;
    private readonly IRoomsManager _roomsManager;
    private readonly IResourcesService _resourcesService;

    public override IReadOnlySet<string> HandledMessageTypes => (HashSet<string>)["uhtml"];

    private uint _lastId;

    public HangmanAnnounceHandler(IConfiguration configuration, IBot bot, IRoomsManager roomsManager,
        IResourcesService resourcesService)
    {
        _configuration = configuration;
        _bot = bot;
        _roomsManager = roomsManager;
        _resourcesService = resourcesService;
    }

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts[1] != "uhtml")
        {
            return Task.CompletedTask;
        }

        var match = HANGMAN_ID_REGEX.Match(parts[2]);
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

        foreach (var (broadcastingRoomId, receivingRoomsIds) in _configuration.EventAnnounces)
        {
            if (roomId != broadcastingRoomId)
            {
                continue;
            }

            foreach (var receivingRoomId in receivingRoomsIds)
            {
                var room = _roomsManager.GetRoom(receivingRoomId);
                var culture = room?.Culture ?? new CultureInfo(_configuration.DefaultLocaleCode);
                var message = string.Format(
                    _resourcesService.GetString("hangman_started_in", culture),
                    broadcastingRoomId);
                _bot.Say(receivingRoomId, $"/wall {message}");
            }
        }

        return Task.CompletedTask;
    }
}