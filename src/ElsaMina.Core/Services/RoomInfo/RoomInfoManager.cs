using ElsaMina.Core.Services.System;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Core.Services.RoomInfo;

public class RoomInfoManager : IRoomInfoManager
{
    private static readonly TimeSpan CANCEL_DELAY = TimeSpan.FromSeconds(5);

    private readonly IClient _client;
    private readonly PendingQueryRequestsManager<string, RoomInfoDto> _pendingRequestsManager;

    public RoomInfoManager(IClient client, ISystemService systemService)
    {
        _client = client;
        _pendingRequestsManager = new PendingQueryRequestsManager<string, RoomInfoDto>(
            systemService,
            CANCEL_DELAY,
            () => null);
    }

    public Task<RoomInfoDto> GetRoomInfoAsync(string roomId, CancellationToken cancellationToken = default)
    {
        // Keep dashes: battle and groupchat room ids contain them
        var normalizedRoomId = roomId.Trim().ToLowerInvariant();

        _client.Send($"|/cmd roominfo {normalizedRoomId}");

        return _pendingRequestsManager.AddOrReplace(normalizedRoomId, cancellationToken);
    }

    public void HandleReceivedRoomInfo(string message)
    {
        RoomInfoDto dto = null;

        try
        {
            // The server sends "modjoin":true when modjoin is synced with modchat
            message = message.Replace("\"modjoin\":true", "\"modjoin\":\"true\"")
                             .Replace("\"modjoin\": true", "\"modjoin\":\"true\"");

            dto = JsonConvert.DeserializeObject<RoomInfoDto>(message);
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Error while deserializing roominfo json");
        }

        if (dto == null)
        {
            return;
        }

        if (dto.Id != null && _pendingRequestsManager.TryResolve(dto.Id, dto))
        {
            return;
        }

        if (dto.RoomId != null && _pendingRequestsManager.TryResolve(dto.RoomId, dto))
        {
            return;
        }

        _pendingRequestsManager.TryResolveOnlyPending(dto);
    }
}
