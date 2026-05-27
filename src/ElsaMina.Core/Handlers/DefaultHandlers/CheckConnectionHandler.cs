using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.System;
using ElsaMina.Logging;

namespace ElsaMina.Core.Handlers.DefaultHandlers;

public class CheckConnectionHandler : Handler
{
    private static readonly TimeSpan ROOM_JOIN_DELAY = TimeSpan.FromMilliseconds(250);
    
    private readonly IConfiguration _configuration;
    private readonly IClient _client;
    private readonly ISystemService _systemService;

    public CheckConnectionHandler(IConfiguration configuration, IClient client,
        ISystemService systemService)
    {
        _configuration = configuration;
        _client = client;
        _systemService = systemService;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "updateuser" };

    public override async Task HandleReceivedMessageAsync(string[] parts, string roomId = null, CancellationToken cancellationToken = default)
    {
        if (parts.Length >= 2 && parts[1] == "updateuser")
        {
            var name = parts[2][1..];
            if (name.Contains("Guest")) // We need to be logged in to join some rooms
            {
                return;
            }

            if (name.Contains('@'))
            {
                name = name[^2..];
            }

            Log.Information("Connected as : {0}", name);

            foreach (var roomIdToJoin in _configuration.Rooms)
            {
                if (_configuration.RoomBlacklist.Contains(roomIdToJoin))
                {
                    continue;
                }

                await _client.SendAsync($"|/join {roomIdToJoin}", cancellationToken);
                await _systemService.SleepAsync(ROOM_JOIN_DELAY, cancellationToken);
            }

            var avatar = _configuration.Avatar;
            if (!string.IsNullOrEmpty(avatar))
            {
                await _client.SendAsync($"|/avatar {avatar}", cancellationToken);
            }
        }
    }
}