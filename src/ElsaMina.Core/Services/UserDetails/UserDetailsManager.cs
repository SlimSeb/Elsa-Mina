using ElsaMina.Core.Services;
using ElsaMina.Core.Services.System;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Core.Services.UserDetails;

public class UserDetailsManager : IUserDetailsManager
{
    private static readonly TimeSpan CANCEL_DELAY = TimeSpan.FromSeconds(5);

    private readonly IClient _client;
    private readonly PendingQueryRequestsManager<string, UserDetailsDto> _pendingRequestsManager;

    public UserDetailsManager(IClient client, ISystemService systemService)
    {
        _client = client;
        _pendingRequestsManager = new PendingQueryRequestsManager<string, UserDetailsDto>(
            systemService,
            CANCEL_DELAY,
            () => null);
    }

    public Task<UserDetailsDto> GetUserDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        _client.Send($"|/cmd userdetails {userId}");

        return _pendingRequestsManager.AddOrReplace(userId, cancellationToken);
    }

    public void HandleReceivedUserDetails(string message)
    {
        UserDetailsDto dto = null;

        try
        {
            message = message.Replace("\"rooms\":false", "")
                             .Replace("\"rooms\": false", "");

            dto = JsonConvert.DeserializeObject<UserDetailsDto>(message);
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Error while deserializing userdata json");
        }

        if (dto == null)
        {
            return;
        }

        _pendingRequestsManager.TryResolve(dto.UserId, dto);
    }
}
