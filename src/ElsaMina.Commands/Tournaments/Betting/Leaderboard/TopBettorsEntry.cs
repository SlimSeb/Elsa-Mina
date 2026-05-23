namespace ElsaMina.Commands.Tournaments.Betting.Leaderboard;

public record TopBettorsEntry(
    int Rank,
    string UserId,
    string UserName,
    int CorrectBetsCount,
    int TotalBetsCount);
