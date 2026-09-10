using ElsaMina.Core.Services.System;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;
using System.Text.Json;

namespace ElsaMina.Core.Services.BattleTracker;

public class ActiveBattlesManager : IActiveBattlesManager
{
    private static readonly TimeSpan CANCEL_DELAY = TimeSpan.FromSeconds(5);

    private readonly IClient _client;
    private readonly PendingQueryRequestsManager<string, IReadOnlyCollection<ActiveBattleDto>> _pendingRequestsManager;

    public ActiveBattlesManager(IClient client, ISystemService systemService)
    {
        _client = client;
        _pendingRequestsManager = new PendingQueryRequestsManager<string, IReadOnlyCollection<ActiveBattleDto>>(
            systemService,
            CANCEL_DELAY,
            Array.Empty<ActiveBattleDto>);
    }

    public Task<IReadOnlyCollection<ActiveBattleDto>> GetActiveBattlesAsync(string format, int minimumElo = 0,
        string prefixFilter = "", CancellationToken cancellationToken = default)
    {
        var minimumEloFilter = minimumElo <= 0 ? "none" : minimumElo.ToString();
        var formatId = format.ToLowerAlphaNum();

        _client.Send($"|/cmd roomlist {formatId},{minimumEloFilter},{prefixFilter}");

        return _pendingRequestsManager.AddOrReplace(formatId, cancellationToken);
    }

    public void HandleReceivedRoomList(string message)
    {
        RoomListQueryResponseDto dto = null;

        try
        {
            dto = JsonSerializer.Deserialize<RoomListQueryResponseDto>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Error while deserializing roomlist json");
        }

        if (dto?.Rooms == null)
        {
            return;
        }

        var battles = dto.Rooms
            .Select(pair => new ActiveBattleDto
            {
                RoomId = pair.Key,
                Player1 = pair.Value?.Player1,
                Player2 = pair.Value?.Player2,
                MinElo = pair.Value?.MinElo
            })
            .ToArray();

        var roomListFormat = GetRoomListFormat(dto.Rooms.Keys);
        if (!string.IsNullOrWhiteSpace(roomListFormat)
            && _pendingRequestsManager.TryResolve(roomListFormat, battles))
        {
            return;
        }

        _pendingRequestsManager.TryResolveOnlyPending(battles);
    }

    private static string GetRoomListFormat(IEnumerable<string> roomIds)
    {
        var formats = roomIds
            .Select(ParseFormatFromBattleRoom)
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Distinct()
            .ToArray();

        return formats.Length == 1 ? formats[0] : null;
    }

    private static string ParseFormatFromBattleRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("battle-"))
        {
            return null;
        }

        const string battlePrefix = "battle-";
        var lastDash = roomId.LastIndexOf('-');

        if (lastDash <= battlePrefix.Length)
        {
            return null;
        }

        return roomId[battlePrefix.Length..lastDash].ToLowerAlphaNum();
    }
}
