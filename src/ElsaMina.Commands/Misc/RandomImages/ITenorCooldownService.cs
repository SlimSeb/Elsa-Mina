namespace ElsaMina.Commands.Misc.RandomImages;

public interface ITenorCooldownService
{
    (TimeSpan RoomRemaining, TimeSpan UserRemaining) GetRemainingCooldowns(string roomId, string userId,
        DateTimeOffset now);

    void SetCooldown(string roomId, string userId, DateTimeOffset now);
}
