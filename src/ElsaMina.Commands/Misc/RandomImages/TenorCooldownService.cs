using System.Collections.Concurrent;

namespace ElsaMina.Commands.Misc.RandomImages;

public class TenorCooldownService : ITenorCooldownService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _roomCooldowns = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _userCooldowns = new();

    public (TimeSpan RoomRemaining, TimeSpan UserRemaining) GetRemainingCooldowns(string roomId, string userId,
        DateTimeOffset now)
    {
        var roomRemaining = TimeSpan.Zero;
        if (_roomCooldowns.TryGetValue(roomId, out var lastRoomUse))
        {
            roomRemaining = TenorConstants.PER_ROOM_COOLDOWN - (now - lastRoomUse);
            if (roomRemaining < TimeSpan.Zero) roomRemaining = TimeSpan.Zero;
        }

        var userRemaining = TimeSpan.Zero;
        if (_userCooldowns.TryGetValue(userId, out var lastUserUse))
        {
            userRemaining = TenorConstants.PER_USER_COOLDOWN - (now - lastUserUse);
            if (userRemaining < TimeSpan.Zero) userRemaining = TimeSpan.Zero;
        }

        return (roomRemaining, userRemaining);
    }

    public void SetCooldown(string roomId, string userId, DateTimeOffset now)
    {
        _roomCooldowns[roomId] = now;
        _userCooldowns[userId] = now;
    }
}
