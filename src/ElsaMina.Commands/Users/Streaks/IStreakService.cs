namespace ElsaMina.Commands.Users.Streaks;

public interface IStreakService
{
    Task UpdateStreakAsync(string userId, string roomId, DateOnly activityDate,
        CancellationToken cancellationToken = default);

    Task<(int CurrentStreak, int LongestStreak)> GetStreakAsync(string userId, string roomId,
        CancellationToken cancellationToken = default);
}
