using System.Collections.Concurrent;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.UserData;

public class UserDataService : IUserDataService
{
    private const string USER_DATA_URL = "https://pokemonshowdown.com/users/{0}.json";

    private readonly ConcurrentDictionary<string, UserDataDto> _userDataCache = new();

    private readonly IHttpService _httpService;

    public UserDataService(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<UserDataDto> GetUserData(string userName, CancellationToken cancellationToken = default)
    {
        var userId = userName.ToLowerAlphaNum();
        if (_userDataCache.TryGetValue(userId, out var cachedUserData))
        {
            return cachedUserData;
        }
        var uri = string.Format(USER_DATA_URL, userId);
        try
        {
            var response = await _httpService.SendAsync<UserDataDto>(HttpRequest.Get(uri), cancellationToken);
            var userData = response.Data;
            _userDataCache[userId] = userData;
            return userData;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while getting user data");
            return null;
        }
    }

    public async Task<DateTimeOffset> GetRegisterDateAsync(string userName,
        CancellationToken cancellationToken = default)
    {
        var userData = await GetUserData(userName, cancellationToken); // todo : cache validity
        return userData == null
            ? DateTimeOffset.MinValue
            : DateTimeOffset.FromUnixTimeSeconds(userData.RegisterTime);
    }
}