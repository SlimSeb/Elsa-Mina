namespace ElsaMina.Battles;

/// <summary>
/// Sends a command that uses the bot's current team (/search, /accept, /vtm) right after setting that team. Showdown
/// keeps one team per user, and searches, challenges and validations each set their own.
/// </summary>
public interface IBattleTeamSender
{
    /// <summary>
    /// /utm <paramref name="packedTeam"/> ("null" without one), then <paramref name="command"/>, with no other team set
    /// in between.
    /// </summary>
    void SendWithTeam(string packedTeam, string command);
}
