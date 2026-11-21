namespace ElsaMina.Battles;

/// <summary>
/// Sends a command that uses the bot's current team (/search, /accept, /vtm, /tour ready) right after setting that
/// team. Showdown keeps one team per user, and searches, challenges, validations and tournament matches each set their
/// own.
/// </summary>
public interface IBattleTeamSender
{
    /// <summary>
    /// /utm <paramref name="packedTeam"/> ("null" without one), then <paramref name="command"/>, with no other team set
    /// in between. The command goes to <paramref name="roomId"/> (a tournament's room), globally without one.
    /// </summary>
    void SendWithTeam(string packedTeam, string command, string roomId = null);
}
