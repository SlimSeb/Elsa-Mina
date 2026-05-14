namespace ElsaMina.Commands.Users.Streaks;

public record StreakLeaderboardEntry(int Rank, string UserId, string UserName, int CurrentStreak, int LongestStreak);
