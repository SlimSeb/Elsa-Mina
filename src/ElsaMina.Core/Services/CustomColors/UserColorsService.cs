using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.CustomColors;

public class UserColorsService : IUserColorsService
{
    private readonly IRoomColorsCache _roomColorsCache;
    private readonly ICustomColorsManager _customColorsManager;

    public UserColorsService(IRoomColorsCache roomColorsCache, ICustomColorsManager customColorsManager)
    {
        _roomColorsCache = roomColorsCache;
        _customColorsManager = customColorsManager;
    }

    public string GetUserColor(string userName)
    {
        var userId = userName.ToLowerAlphaNum();

        var nameColor = _roomColorsCache.GetColor(userId);
        if (nameColor != null)
        {
            return nameColor;
        }

        if (_customColorsManager.CustomColorsMapping.TryGetValue(userId, out var userCustomColor))
        {
            userId = userCustomColor;
        }

        return userId.ToColor().ToHexString();
    }
}
