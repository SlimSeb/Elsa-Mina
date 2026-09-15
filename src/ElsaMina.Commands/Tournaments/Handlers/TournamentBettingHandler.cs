using System.Collections.Concurrent;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;
using System.Text.Json;

namespace ElsaMina.Commands.Tournaments.Handlers;

public class TournamentBettingHandler : Handler
{
    private readonly ConcurrentDictionary<string, TournamentPlayer[]> _pendingPlayers = new();
    private readonly ITournamentBettingService _tournamentBettingService;
    private readonly IRoomsManager _roomsManager;

    public TournamentBettingHandler(ITournamentBettingService tournamentBettingService, IRoomsManager roomsManager)
    {
        _tournamentBettingService = tournamentBettingService;
        _roomsManager = roomsManager;
    }

    public override IReadOnlySet<string> HandledMessageTypes => (HashSet<string>)["tournament"];

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public override async Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (roomId == null || parts.Length < 3)
        {
            return;
        }

        switch (parts[2])
        {
            case "update" when parts.Length >= 4:
                UpdatePendingPlayers(roomId, parts[3]);
                break;
            case "start":
                await StartBettingAsync(roomId, cancellationToken);
                break;
            case "forceend":
                _pendingPlayers.Remove(roomId, out _);
                await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
                break;
            case "end" when parts.Length >= 4:
                await ResolveBettingAsync(roomId, parts[3], cancellationToken);
                break;
            default:
                // Any other tournament update is irrelevant for betting.
                break;
        }
    }

    private void UpdatePendingPlayers(string roomId, string rawUpdate)
    {
        var sanitizedJson = rawUpdate.Replace(@"\'", "'");
        var update = JsonSerializer.Deserialize<TournamentUpdate>(sanitizedJson, JSON_OPTIONS);
        var incomingUsers = update?.BracketData?.Users;
        if (incomingUsers == null || incomingUsers.Length == 0)
        {
            return;
        }

        _pendingPlayers[roomId] = incomingUsers
            .Select(username => new TournamentPlayer(username.ToLowerAlphaNum(), username))
            .DistinctBy(player => player.UserId)
            .ToArray();
    }

    private async Task StartBettingAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = _roomsManager.GetRoom(roomId);
        var isBettingEnabled = room == null ||
                               (await room.GetParameterValueAsync(Parameter.TournamentBettingEnabled,
                                   cancellationToken)).ToBoolean();
        if (!isBettingEnabled)
        {
            _pendingPlayers.Remove(roomId, out _);
            return;
        }

        if (!_pendingPlayers.TryGetValue(roomId, out var users))
        {
            return;
        }

        _pendingPlayers.Remove(roomId, out _);
        await _tournamentBettingService.AnnounceBetsAsync(users, roomId, cancellationToken);
    }

    private async Task ResolveBettingAsync(string roomId, string rawResults, CancellationToken cancellationToken)
    {
        _pendingPlayers.Remove(roomId, out _);

        try
        {
            var result = TournamentHelper.ParseTourResults(rawResults);
            if (result?.Winner != null)
            {
                await _tournamentBettingService.ResolveBetsAsync(result.Winner, roomId, cancellationToken);
            }
            else
            {
                await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error resolving bets for room {RoomId}", roomId);
            await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
        }
    }
}